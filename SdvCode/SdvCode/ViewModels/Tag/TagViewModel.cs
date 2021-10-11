﻿// Copyright (c) SDV Code Project. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace SdvCode.ViewModels.Tag
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using SdvCode.Models.Blog;
    using SdvCode.ViewModels.Post.ViewModels;

    public class TagViewModel
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public DateTime CreatedOn { get; set; }

        // TODO
        public Tag Tag { get; set; }

        public ICollection<PostViewModel> Posts { get; set; } = new HashSet<PostViewModel>();
    }
}