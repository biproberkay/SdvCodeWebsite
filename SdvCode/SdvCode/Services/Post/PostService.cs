﻿// Copyright (c) SDV Code Project. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace SdvCode.Services.Post
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.SignalR;
    using Microsoft.EntityFrameworkCore;

    using SdvCode.Areas.Administration.Models.Enums;
    using SdvCode.Areas.UserNotifications.Services;
    using SdvCode.Constraints;
    using SdvCode.Data;
    using SdvCode.Hubs;
    using SdvCode.Models.Blog;
    using SdvCode.Models.Enums;
    using SdvCode.Models.User;
    using SdvCode.ViewModels.Post.ViewModels;

    public class PostService : IPostService
    {
        private readonly ApplicationDbContext db;
        private readonly UserManager<ApplicationUser> userManager;
        private readonly IHubContext<NotificationHub> notificationHubContext;
        private readonly INotificationService notificationService;
        private readonly AddCyclicActivity cyclicActivity;

        public PostService(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IHubContext<NotificationHub> notificationHubContext,
            INotificationService notificationService)
        {
            this.db = db;
            this.userManager = userManager;
            this.notificationHubContext = notificationHubContext;
            this.notificationService = notificationService;
            this.cyclicActivity = new AddCyclicActivity(this.db);
        }

        public async Task<Tuple<string, string>> AddToFavorite(ApplicationUser user, string id)
        {
            if (user != null && id != null)
            {
                if (this.db.FavouritePosts.Any(x => x.PostId == id && x.ApplicationUserId == user.Id))
                {
                    this.db.FavouritePosts.FirstOrDefault(x => x.PostId == id && x.ApplicationUserId == user.Id).IsFavourite = true;
                }
                else
                {
                    this.db.FavouritePosts.Add(new FavouritePost
                    {
                        ApplicationUserId = user.Id,
                        PostId = id,
                        IsFavourite = true,
                    });
                }

                await this.db.SaveChangesAsync();

                var post = await this.db.Posts.FirstOrDefaultAsync(x => x.Id == id);

                if (post.ApplicationUserId != user.Id)
                {
                    var targetUser = await this.db.Users
                            .FirstOrDefaultAsync(x => x.Id == post.ApplicationUserId);
                    string notificationForApprovingId =
                           await this.notificationService
                           .AddPostToFavoriteNotification(targetUser, user, post.ShortContent, post.Id);

                    var targetUserNotificationsCount = await this.notificationService.GetUserNotificationsCount(targetUser.UserName);
                    await this.notificationHubContext
                        .Clients
                        .User(targetUser.Id)
                        .SendAsync("ReceiveNotification", targetUserNotificationsCount, true);

                    var notificationForApproving = await this.notificationService.GetNotificationById(notificationForApprovingId);
                    await this.notificationHubContext.Clients.User(targetUser.Id)
                        .SendAsync("VisualizeNotification", notificationForApproving);
                }

                return Tuple.Create("Success", SuccessMessages.SuccessfullyAddedToFavorite);
            }

            return Tuple.Create("Error", ErrorMessages.InvalidInputModel);
        }

        public async Task<PostViewModel> ExtractCurrentPost(string id, ApplicationUser user)
        {
            var post = await this.db.Posts.FirstOrDefaultAsync(x => x.Id == id);

            if (await this.userManager.IsInRoleAsync(user, Roles.Administrator.ToString()) ||
                await this.userManager.IsInRoleAsync(user, Roles.Editor.ToString()))
            {
                post.Comments = this.db.Comments.Where(x => x.PostId == post.Id).OrderBy(x => x.CreatedOn).ToList();
            }
            else
            {
                var targetComments = this.db.Comments
                    .Where(x => x.PostId == post.Id)
                    .OrderBy(x => x.CreatedOn)
                    .ToList();
                List<Comment> comments = new List<Comment>();

                foreach (var comment in targetComments)
                {
                    if (comment.CommentStatus == CommentStatus.Pending && comment.ApplicationUserId == user.Id)
                    {
                        comments.Add(comment);
                    }
                    else
                    {
                        if (comment.CommentStatus == CommentStatus.Approved)
                        {
                            comments.Add(comment);
                        }
                    }
                }

                post.Comments = comments;
            }

            foreach (var comment in post.Comments)
            {
                comment.ApplicationUser = this.db.Users.FirstOrDefault(x => x.Id == comment.ApplicationUserId);
            }

            post.PostsTags = this.db.PostsTags.Where(x => x.PostId == post.Id).ToList();
            PostViewModel model = new PostViewModel
            {
                Id = post.Id,
                Title = post.Title,
                Likes = post.Likes,
                Content = post.Content,
                CreatedOn = post.CreatedOn,
                UpdatedOn = post.UpdatedOn,
                //Comments = post.Comments,
                ImageUrl = post.ImageUrl,
                IsLiked = this.db.PostsLikes.Any(x => x.PostId == id && x.UserId == user.Id && x.IsLiked == true),
                IsAuthor = post.ApplicationUserId == user.Id,
                IsFavourite = this.db.FavouritePosts.Any(x => x.ApplicationUserId == user.Id && x.PostId == post.Id && x.IsFavourite == true),
                PostStatus = post.PostStatus,
            };

            //model.ApplicationUser = this.db.Users.FirstOrDefault(x => x.Id == post.ApplicationUserId);
            //model.Category = this.db.Categories.FirstOrDefault(x => x.Id == post.CategoryId);

            foreach (var tag in post.PostsTags)
            {
                var curretnTag = this.db.Tags.FirstOrDefault(x => x.Id == tag.TagId);
                //model.Tags.Add(new Tag
                //{
                //    Id = curretnTag.Id,
                //    CreatedOn = curretnTag.CreatedOn,
                //    Name = curretnTag.Name,
                //});
            }

            var usersIds = this.db.PostsLikes
                .Where(x => x.PostId == post.Id && x.IsLiked == true)
                .Select(x => x.UserId)
                .ToList();
            foreach (var userId in usersIds)
            {
                //model.Likers.Add(this.db.Users.FirstOrDefault(x => x.Id == userId));
            }

            var allPostImages = this.db.PostImages
                    .Where(x => x.PostId == post.Id)
                    .OrderBy(x => x.Name)
                    .ToList();
            foreach (var postImage in allPostImages)
            {
                model.PostImages.Add(new PostImageViewModel
                {
                    Id = postImage.Id,
                    Name = postImage.Name,
                    Url = postImage.Url,
                });
            }

            return model;
        }

        public async Task<bool> IsPostExist(string id)
        {
            return await this.db.Posts.AnyAsync(x => x.Id == id);
        }

        public async Task<Tuple<string, string>> LikePost(string id, ApplicationUser user)
        {
            var post = this.db.Posts.FirstOrDefault(x => x.Id == id);
            post.ApplicationUser = this.db.Users.Find(post.ApplicationUserId);

            if (post != null)
            {
                post.Likes++;
                this.db.Posts.Update(post);
                var targetLike = this.db.PostsLikes.FirstOrDefault(x => x.PostId == id && x.UserId == user.Id);

                if (targetLike != null && targetLike.IsLiked == false)
                {
                    targetLike.IsLiked = true;
                }
                else if (targetLike != null && targetLike.IsLiked == true)
                {
                    targetLike.IsLiked = false;
                }
                else
                {
                    this.db.PostsLikes.Add(new PostLike
                    {
                        UserId = user.Id,
                        PostId = id,
                        IsLiked = true,
                    });
                }

                if (post.ApplicationUserId == user.Id)
                {
                    this.cyclicActivity.AddLikeUnlikeActivity(user, post, UserActionsType.LikeOwnPost, user);
                }
                else
                {
                    this.cyclicActivity.AddLikeUnlikeActivity(post.ApplicationUser, post, UserActionsType.LikedPost, user);
                    this.cyclicActivity.AddLikeUnlikeActivity(user, post, UserActionsType.LikePost, post.ApplicationUser);
                }

                await this.db.SaveChangesAsync();
                return Tuple.Create("Success", SuccessMessages.SuccessfullyLikePost);
            }

            return Tuple.Create("Error", ErrorMessages.InvalidInputModel);
        }

        public async Task<Tuple<string, string>> RemoveFromFavorite(ApplicationUser user, string id)
        {
            if (user != null && id != null)
            {
                if (this.db.FavouritePosts.Any(x => x.PostId == id && x.ApplicationUserId == user.Id))
                {
                    this.db.FavouritePosts.FirstOrDefault(x => x.PostId == id && x.ApplicationUserId == user.Id).IsFavourite = false;
                }
                else
                {
                    return Tuple.Create("Error", ErrorMessages.InvalidInputModel);
                }

                await this.db.SaveChangesAsync();
                return Tuple.Create("Success", SuccessMessages.SuccessfullyRemoveFromFavorite);
            }

            return Tuple.Create("Error", ErrorMessages.InvalidInputModel);
        }

        public async Task<Tuple<string, string>> UnlikePost(string id, ApplicationUser user)
        {
            var post = this.db.Posts.FirstOrDefault(x => x.Id == id);
            post.ApplicationUser = this.db.Users.Find(post.ApplicationUserId);

            var targetPostsLikes = this.db.PostsLikes.FirstOrDefault(x => x.PostId == id && x.UserId == user.Id);
            if (targetPostsLikes != null && targetPostsLikes.IsLiked == true)
            {
                targetPostsLikes.IsLiked = false;
                post.Likes--;

                if (post.ApplicationUserId == user.Id)
                {
                    this.cyclicActivity.AddLikeUnlikeActivity(user, post, UserActionsType.UnlikeOwnPost, user);
                }
                else
                {
                    this.cyclicActivity.AddLikeUnlikeActivity(post.ApplicationUser, post, UserActionsType.UnlikedPost, user);
                    this.cyclicActivity.AddLikeUnlikeActivity(user, post, UserActionsType.UnlikePost, post.ApplicationUser);
                }

                await this.db.SaveChangesAsync();
                return Tuple.Create("Success", SuccessMessages.SuccessfullyUnlikePost);
            }

            return Tuple.Create("Error", ErrorMessages.InvalidInputModel);
        }
    }
}