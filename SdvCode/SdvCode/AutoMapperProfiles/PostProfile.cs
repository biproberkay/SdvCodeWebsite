﻿// Copyright (c) SDV Code Project. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace SdvCode.AutoMapperProfiles
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Claims;
    using System.Threading.Tasks;

    using AutoMapper;

    using Microsoft.AspNetCore.Http;

    using SdvCode.Models.Blog;
    using SdvCode.Models.User;
    using SdvCode.ViewModels.Blog.ViewModels.BlogPostCard;
    using SdvCode.ViewModels.Category;
    using SdvCode.ViewModels.Comment.ViewModels;
    using SdvCode.ViewModels.Tag;

    public class PostProfile : Profile
    {
        private readonly IHttpContextAccessor httpContextAccessor;

        public PostProfile(IHttpContextAccessor httpContextAccessor)
        {
            this.httpContextAccessor = httpContextAccessor;
            var userId = this.httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

            this.CreateMap<Category, BlogPostCardCategoryViewModel>();
            this.CreateMap<ApplicationUser, BlogPostCardLikerViewModel>();
            this.CreateMap<Post, BlogPostCardViewModel>()
                .ForMember(
                    dm => dm.CommentsCount,
                    mo => mo.MapFrom(x => x.Comments.Count))
                .ForMember(
                    dm => dm.IsLiked,
                    mo => mo.MapFrom(x => userId != null && x.PostLikes.Any(y => y.UserId == userId && y.IsLiked)))
                .ForMember(
                    dm => dm.IsAuthor,
                    mo => mo.MapFrom(x => userId != null && userId == x.ApplicationUserId))
                .ForMember(
                    dm => dm.IsFavourite,
                    mo => mo.MapFrom(x => userId != null && x.FavouritePosts.Any(z => z.ApplicationUserId == userId && z.IsFavourite)))
                .ForMember(
                    dm => dm.Likers,
                    mo => mo.MapFrom(x => x.PostLikes.Select(x => x.ApplicationUser).ToList()));
        }
    }
}