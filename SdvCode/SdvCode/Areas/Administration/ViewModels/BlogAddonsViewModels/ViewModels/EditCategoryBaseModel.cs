﻿namespace SdvCode.Areas.Administration.ViewModels.BlogAddonsViewModels.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using SdvCode.Areas.Administration.ViewModels.BlogAddonsViewModels.InputModels;
    using SdvCode.Areas.Editor.ViewModels;

    public class EditCategoryBaseModel
    {
        public ICollection<EditCategoryViewModel> EditCategoryViewModels { get; set; } =
            new HashSet<EditCategoryViewModel>();

        public EditCategoryInputModel EditCategoryInputModel { get; set; } = new EditCategoryInputModel();
    }
}