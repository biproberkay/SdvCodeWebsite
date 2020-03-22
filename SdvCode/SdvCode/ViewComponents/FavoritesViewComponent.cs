﻿// Copyright (c) SDV Code Project. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace SdvCode.ViewComponents
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using SdvCode.Constraints;
    using SdvCode.Models.User;
    using SdvCode.Services.Profile.Pagination;
    using SdvCode.ViewModels.Pagination;
    using SdvCode.ViewModels.Profile;
    using X.PagedList;

    public class FavoritesViewComponent : ViewComponent
    {
        private readonly UserManager<ApplicationUser> userManager;
        private readonly IProfileFavoritesService favoritesService;

        public FavoritesViewComponent(UserManager<ApplicationUser> userManager, IProfileFavoritesService favoritesService)
        {
            this.userManager = userManager;
            this.favoritesService = favoritesService;
        }

        public async Task<IViewComponentResult> InvokeAsync(string username, int page)
        {
            var user = await this.userManager.FindByNameAsync(username);
            List<FavoritesViewModel> allFollowers = await this.favoritesService.ExtractFavorites(user, this.HttpContext);

            FavoritesPaginationViewModel model = new FavoritesPaginationViewModel
            {
                Username = username,
                Favorites = allFollowers.ToPagedList(page, GlobalConstants.FavoritesCountOnPage),
            };

            return this.View(model);
        }
    }
}