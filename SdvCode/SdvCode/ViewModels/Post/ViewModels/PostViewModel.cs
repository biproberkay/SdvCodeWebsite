﻿// Copyright (c) SDV Code Project. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace SdvCode.ViewModels.Post.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using SdvCode.Models.Blog;
    using SdvCode.Models.Enums;
    using SdvCode.Models.User;

    public class PostViewModel
    {
        public string Id { get; set; }

        public string Title { get; set; }

        public string Content { get; set; }

        public string ShortContent { get; set; }

        public DateTime CreatedOn { get; set; }

        public DateTime UpdatedOn { get; set; }

        public string ImageUrl { get; set; }

        public int Likes { get; set; }

        public string ApplicationUserId { get; set; }

        public ApplicationUser ApplicationUser { get; set; }

        public string CategoryId { get; set; }

        public Category Category { get; set; }

        public PostStatus PostStatus { get; set; }

        public bool IsFavourite { get; set; }

        public ICollection<Comment> Comments { get; set; } = new HashSet<Comment>();

        public ICollection<Tag> Tags { get; set; } = new HashSet<Tag>();

        public bool IsAuthor { get; set; }

        public bool IsLiked { get; set; }

        public ICollection<ApplicationUser> Likers { get; set; } = new HashSet<ApplicationUser>();

        public ICollection<FavouritePost> FavouritePosts { get; set; } = new HashSet<FavouritePost>();

        public ICollection<PendingPost> PendingPosts { get; set; } = new HashSet<PendingPost>();

        public ICollection<PostImageViewModel> AllPostImages { get; set; } = new HashSet<PostImageViewModel>();
    }
}