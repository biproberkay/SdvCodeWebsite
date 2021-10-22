﻿// Copyright (c) SDV Code Project. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace SdvCode.Services.Profile.Pagination.Profile
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using SdvCode.Models.User;
    using SdvCode.ViewModels.Profile;
    using SdvCode.ViewModels.Profile.UserViewComponents;
    using SdvCode.ViewModels.Profile.UserViewComponents.ActivitiesComponent;

    public interface IProfileFollowersService
    {
        List<FollowersViewModel> ExtractFollowers(ApplicationUser user, string currentUserId);
    }
}