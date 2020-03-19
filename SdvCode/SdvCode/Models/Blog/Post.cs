﻿// Copyright (c) SDV Code Project. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace SdvCode.Models.Blog
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc.Formatters;
    using SdvCode.Models.User;

    public class Post
    {
        public Post()
        {
            this.Id = Guid.NewGuid().ToString();
        }

        [Key]
        public string Id { get; set; }

        [Required]
        [MaxLength(150)]
        public string Title { get; set; }

        [Required]
        public string Content { get; set; }

        [Required]
        [MaxLength(350)]
        public string ShortContent { get; set; }

        [Required]
        public DateTime CreatedOn { get; set; }

        public DateTime UpdatedOn { get; set; }

        public string ImageUrl { get; set; }

        public int Likes { get; set; }

        [Required]
        [ForeignKey(nameof(ApplicationUser))]
        public string ApplicationUserId { get; set; }

        public ApplicationUser ApplicationUser { get; set; }

        [Required]
        [ForeignKey(nameof(Category))]
        public string CategoryId { get; set; }

        public Category Category { get; set; }

        public ICollection<Comment> Comments { get; set; } = new HashSet<Comment>();

        public ICollection<PostTag> PostsTags { get; set; } = new HashSet<PostTag>();

        [NotMapped]
        public bool IsLiked { get; set; }

        [NotMapped]
        public ICollection<ApplicationUser> Likers { get; set; } = new HashSet<ApplicationUser>();
    }
}