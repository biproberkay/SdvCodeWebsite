﻿// Copyright (c) SDV Code Project. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace SdvCode.Services.Profile.Pagination.AllUsers.RecommendedUsers
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using Microsoft.EntityFrameworkCore;

    using SdvCode.Data;
    using SdvCode.Models.User;
    using SdvCode.ViewModels.Users.ViewModels;

    public class RecommendedUsersService : IRecommendedUsersService
    {
        private readonly ApplicationDbContext db;

        public RecommendedUsersService(ApplicationDbContext db)
        {
            this.db = db;
        }

        public async Task<List<AllUsersUserCardViewModel>> ExtractAllUsers(string username, string search)
        {
            List<AllUsersUserCardViewModel> allUsers = new List<AllUsersUserCardViewModel>();
            var user = await this.db.Users.FirstOrDefaultAsync(x => x.UserName == username);

            var targetUsers = new List<RecommendedFriend>();

            if (search == null)
            {
                targetUsers = await this.db.RecommendedFriends
                    .Where(x => x.ApplicationUserId == user.Id)
                    .ToListAsync();
            }
            else
            {
                targetUsers = await this.db.RecommendedFriends
                     .Where(x => (EF.Functions.FreeText(x.RecommendedUsername, search) ||
                     EF.Functions.FreeText(x.RecommendedFirstName, search) ||
                     EF.Functions.FreeText(x.RecommendedLastName, search)) &&
                     x.ApplicationUserId == user.Id)
                     .ToListAsync();
            }

            foreach (var targetUser in targetUsers)
            {
                var recommendedUser = await this.db.Users.FirstOrDefaultAsync(x => x.UserName == targetUser.RecommendedUsername);
                allUsers.Add(new AllUsersUserCardViewModel
                {
                    Id = recommendedUser.Id,
                    UserName = targetUser.RecommendedUsername,
                    FirstName = targetUser.RecommendedFirstName,
                    LastName = targetUser.RecommendedLastName,
                    ImageUrl = targetUser.RecommendedImageUrl,
                    CoverImageUrl = targetUser.RecommendedCoverImage,
                });
            }

            foreach (var targetUser in allUsers)
            {
                targetUser.FollowingsCount = await this.db.FollowUnfollows
                    .CountAsync(x => x.FollowerId == targetUser.Id && x.IsFollowed == true);

                targetUser.FollowersCount = await this.db.FollowUnfollows
                    .CountAsync(x => x.ApplicationUserId == targetUser.Id && x.IsFollowed == true);

                targetUser.HasFollowed = await this.db.FollowUnfollows
                    .AnyAsync(x => x.FollowerId == user.Id && x.ApplicationUserId == targetUser.Id && x.IsFollowed == true);

                targetUser.Activities = await this.db.UserActions
                    .CountAsync(x => x.ApplicationUserId == targetUser.Id);
            }

            return allUsers;
        }
    }
}