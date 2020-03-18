﻿// Copyright (c) SDV Code Project. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace SdvCode.ViewModels.Tag
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using SdvCode.Models.Blog;

    public class TagViewModel
    {
        public Tag Tag { get; set; }

        public ICollection<Post> Posts { get; set; } = new HashSet<Post>();
    }
}