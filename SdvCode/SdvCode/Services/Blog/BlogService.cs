﻿// Copyright (c) SDV Code Project. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace SdvCode.Services.Blog
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using CloudinaryDotNet;
    using Ganss.XSS;
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
    using SdvCode.Services.Cloud;
    using SdvCode.ViewModels.Blog.InputModels;
    using SdvCode.ViewModels.Blog.ViewModels;
    using SdvCode.ViewModels.Post.InputModels;
    using SdvCode.ViewModels.Post.ViewModels;

    public class BlogService : UserValidationService, IBlogService
    {
        private readonly ApplicationDbContext db;
        private readonly Cloudinary cloudinary;
        private readonly UserManager<ApplicationUser> userManager;
        private readonly INotificationService notificationService;
        private readonly IHubContext<NotificationHub> notificationHubContext;
        private readonly GlobalPostsExtractor postExtractor;
        private readonly AddCyclicActivity cyclicActivity;
        private readonly AddNonCyclicActivity nonCyclicActivity;

        public BlogService(
            ApplicationDbContext db,
            Cloudinary cloudinary,
            UserManager<ApplicationUser> userManager,
            INotificationService notificationService,
            IHubContext<NotificationHub> notificationHubContext)
            : base(userManager, db)
        {
            this.db = db;
            this.cloudinary = cloudinary;
            this.userManager = userManager;
            this.notificationService = notificationService;
            this.notificationHubContext = notificationHubContext;
            this.postExtractor = new GlobalPostsExtractor(this.db);
            this.cyclicActivity = new AddCyclicActivity(this.db);
            this.nonCyclicActivity = new AddNonCyclicActivity(this.db);
        }

        public async Task<Tuple<string, string>> CreatePost(CreatePostIndexModel model, ApplicationUser user)
        {
            var category = this.db.Categories.FirstOrDefault(x => x.Name == model.PostInputModel.CategoryName);
            var contentWithoutTags = Regex.Replace(model.PostInputModel.SanitizeContent, "<.*?>", string.Empty);

            var post = new Post
            {
                Title = model.PostInputModel.Title,
                CategoryId = category.Id,
                Content = model.PostInputModel.SanitizeContent,
                CreatedOn = DateTime.UtcNow,
                UpdatedOn = DateTime.UtcNow,
                ShortContent = contentWithoutTags.Length <= 347 ?
                    contentWithoutTags :
                    $"{contentWithoutTags.Substring(0, 347)}...",
                ApplicationUserId = user.Id,
                Likes = 0,
            };

            var imageUrl = await ApplicationCloudinary.UploadImage(
                this.cloudinary,
                model.PostInputModel.CoverImage,
                string.Format(GlobalConstants.CloudinaryPostCoverImageName, post.Id),
                GlobalConstants.PostBaseImageFolder);

            if (imageUrl != null)
            {
                post.ImageUrl = imageUrl;
            }

            foreach (var tagName in model.PostInputModel.TagsNames)
            {
                var tag = this.db.Tags.FirstOrDefault(x => x.Name.ToLower() == tagName.ToLower());
                post.PostsTags.Add(new PostTag
                {
                    PostId = post.Id,
                    TagId = tag.Id,
                });
            }

            var adminRole =
                await this.db.Roles.FirstOrDefaultAsync(x => x.Name == Roles.Administrator.ToString());
            var editorRole =
                await this.db.Roles.FirstOrDefaultAsync(x => x.Name == Roles.Editor.ToString());

            var allAdminIds = this.db.UserRoles
                .Where(x => x.RoleId == adminRole.Id)
                .Select(x => x.UserId)
                .ToList();
            var allEditorIds = this.db.UserRoles
                .Where(x => x.RoleId == editorRole.Id)
                .Select(x => x.UserId)
                .ToList();
            var specialIds = allAdminIds.Union(allEditorIds).ToList();

            if (await this.userManager.IsInRoleAsync(user, Roles.Administrator.ToString()) ||
                await this.userManager.IsInRoleAsync(user, Roles.Editor.ToString()) ||
                await this.userManager.IsInRoleAsync(user, Roles.Author.ToString()))
            {
                post.PostStatus = PostStatus.Approved;
                var followerIds = this.db.FollowUnfollows
                    .Where(x => x.PersonId == user.Id && !specialIds.Contains(x.FollowerId))
                    .Select(x => x.FollowerId)
                    .ToList();
                specialIds = specialIds.Union(followerIds).ToList();
                specialIds.Remove(user.Id);
            }
            else
            {
                post.PostStatus = PostStatus.Pending;
                this.db.PendingPosts.Add(new PendingPost
                {
                    ApplicationUserId = post.ApplicationUserId,
                    PostId = post.Id,
                    IsPending = true,
                });
            }

            foreach (var specialId in specialIds)
            {
                var toUser = await this.db.Users.FirstOrDefaultAsync(x => x.Id == specialId);

                string notificationId =
                    await this.notificationService.AddBlogPostNotification(toUser, user, post.ShortContent, post.Id);

                var count = await this.notificationService.GetUserNotificationsCount(toUser.UserName);
                await this.notificationHubContext
                    .Clients
                    .User(toUser.Id)
                    .SendAsync("ReceiveNotification", count, true);

                var notification = await this.notificationService.GetNotificationById(notificationId);
                await this.notificationHubContext.Clients.User(toUser.Id)
                    .SendAsync("VisualizeNotification", notification);
            }

            this.db.Posts.Add(post);
            this.db.BlockedPosts.Add(new BlockedPost
            {
                ApplicationUserId = post.ApplicationUserId,
                PostId = post.Id,
                IsBlocked = false,
            });

            this.nonCyclicActivity.AddUserAction(user, post, UserActionsType.CreatePost, user);
            await this.db.SaveChangesAsync();
            return Tuple.Create("Success", SuccessMessages.SuccessfullyCreatedPost);
        }

        public async Task<Tuple<string, string>> DeletePost(string id, ApplicationUser user)
        {
            var post = this.db.Posts.FirstOrDefault(x => x.Id == id);
            var userPost = this.db.Users.FirstOrDefault(x => x.Id == post.ApplicationUserId);

            if (post != null && userPost != null)
            {
                if (post.ImageUrl != null)
                {
                    ApplicationCloudinary.DeleteImage(
                        this.cloudinary,
                        string.Format(GlobalConstants.CloudinaryPostCoverImageName, post.Id),
                        GlobalConstants.PostBaseImageFolder);
                }

                if (user.Id == post.ApplicationUserId)
                {
                    this.cyclicActivity.AddUserAction(user, UserActionsType.DeleteOwnPost, user);
                }
                else
                {
                    this.cyclicActivity.AddUserAction(user, UserActionsType.DeletedPost, userPost);
                    this.cyclicActivity.AddUserAction(userPost, UserActionsType.DeletePost, user);
                }

                var postActivities = this.db.UserActions.Where(x => x.PostId == post.Id);
                var comments = this.db.Comments.Where(x => x.PostId == post.Id).ToList();
                this.db.Comments.RemoveRange(comments);
                this.db.UserActions.RemoveRange(postActivities);
                this.db.Posts.Remove(post);

                await this.db.SaveChangesAsync();
                return Tuple.Create("Success", SuccessMessages.SuccessfullyDeletePost);
            }

            return Tuple.Create("Error", SuccessMessages.SuccessfullyDeletePost);
        }

        public async Task<Tuple<string, string>> EditPost(EditPostInputModel model, ApplicationUser user)
        {
            var post = await this.db.Posts.FirstOrDefaultAsync(x => x.Id == model.Id);
            var contentWithoutTags = Regex.Replace(model.SanitizeContent, "<.*?>", string.Empty);

            if (post != null)
            {
                var category = await this.db.Categories.FirstOrDefaultAsync(x => x.Name == model.CategoryName);
                var postUser = await this.db.Users.FirstOrDefaultAsync(x => x.Id == post.ApplicationUserId);
                post.Category = category;
                post.Title = model.Title;
                post.UpdatedOn = DateTime.UtcNow;
                post.Content = model.SanitizeContent;
                post.ShortContent = contentWithoutTags.Length <= 347 ?
                    contentWithoutTags :
                    $"{contentWithoutTags.Substring(0, 347)}...";

                var imageUrl = await ApplicationCloudinary.UploadImage(
                    this.cloudinary,
                    model.CoverImage,
                    string.Format(
                        GlobalConstants.CloudinaryPostCoverImageName,
                        post.Id),
                    GlobalConstants.PostBaseImageFolder);

                if (imageUrl != null)
                {
                    post.ImageUrl = imageUrl;
                }

                if (model.TagsNames.Count > 0)
                {
                    List<PostTag> oldTagsIds = this.db.PostsTags.Where(x => x.PostId == model.Id).ToList();
                    this.db.PostsTags.RemoveRange(oldTagsIds);

                    List<PostTag> postTags = new List<PostTag>();
                    foreach (var tagName in model.TagsNames)
                    {
                        var tag = await this.db.Tags.FirstOrDefaultAsync(x => x.Name.ToLower() == tagName.ToLower());
                        postTags.Add(new PostTag
                        {
                            PostId = post.Id,
                            TagId = tag.Id,
                        });
                    }

                    post.PostsTags = postTags;
                }

                if (user.Id == postUser.Id)
                {
                    this.nonCyclicActivity.AddUserAction(user, post, UserActionsType.EditOwnPost, user);
                }
                else
                {
                    this.nonCyclicActivity.AddUserAction(user, post, UserActionsType.EditPost, postUser);
                    this.nonCyclicActivity.AddUserAction(postUser, post, UserActionsType.EditedPost, user);
                }

                this.db.Posts.Update(post);
                await this.db.SaveChangesAsync();
                return Tuple.Create("Success", SuccessMessages.SuccessfullyEditedPost);
            }

            return Tuple.Create("Error", ErrorMessages.InvalidInputModel);
        }

        public async Task<ICollection<string>> ExtractAllCategoryNames()
        {
            return await this.db.Categories.Select(x => x.Name).OrderBy(x => x).ToListAsync();
        }

        public async Task<ICollection<string>> ExtractAllTagNames()
        {
            return await this.db.Tags.Select(x => x.Name).OrderBy(x => x).ToListAsync();
        }

        public async Task<EditPostInputModel> ExtractPost(string id, ApplicationUser user)
        {
            var post = await this.db.Posts.FirstOrDefaultAsync(x => x.Id == id);
            post.Category = await this.db.Categories.FirstOrDefaultAsync(x => x.Id == post.CategoryId);
            var postTagsNames = new List<string>();

            foreach (var tag in post.PostsTags)
            {
                postTagsNames.Add(this.db.Tags.FirstOrDefault(x => x.Id == tag.TagId).Name);
            }

            return new EditPostInputModel
            {
                Id = post.Id,
                Title = post.Title,
                CategoryName = post.Category.Name,
                Content = post.Content,
                TagsNames = postTagsNames,
                Tags = postTagsNames,
            };
        }

        public async Task<ICollection<PostViewModel>> ExtraxtAllPosts(ApplicationUser user, string search)
        {
            var posts = new List<Post>();

            if (search == null)
            {
                posts = await this.db.Posts.OrderByDescending(x => x.UpdatedOn).ToListAsync();
            }
            else
            {
                posts = await this.db.Posts
                    .Where(x => EF.Functions.Contains(x.Title, search) ||
                    EF.Functions.Contains(x.ShortContent, search) ||
                    EF.Functions.Contains(x.Content, search))
                    .OrderByDescending(x => x.UpdatedOn)
                    .ToListAsync();
            }

            if (user != null &&
                (await this.userManager.IsInRoleAsync(user, Roles.Administrator.ToString()) ||
                await this.userManager.IsInRoleAsync(user, Roles.Editor.ToString())))
            {
                posts = posts
                    .Where(x => x.PostStatus == PostStatus.Banned || x.PostStatus == PostStatus.Pending || x.PostStatus == PostStatus.Approved)
                    .ToList();
            }
            else
            {
                if (user != null)
                {
                    posts = posts
                        .Where(x => x.PostStatus == PostStatus.Approved ||
                        x.ApplicationUserId == user.Id)
                        .ToList();
                }
                else
                {
                    posts = posts
                        .Where(x => x.PostStatus == PostStatus.Approved)
                        .ToList();
                }
            }

            List<PostViewModel> postsModel = await this.postExtractor.ExtractPosts(user, posts);
            return postsModel;
        }

        public async Task<bool> IsPostExist(string id)
        {
            return await this.db.Posts.AnyAsync(x => x.Id == id);
        }
    }
}