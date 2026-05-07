using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Netor.Cortana.Platform.Admin.Models.Categories;
using Netor.Cortana.Platform.Entitys.Data;
using Netor.Cortana.Platform.Entitys.Tables.Assets;

namespace Netor.Cortana.Platform.Admin.Controllers;

public sealed class CategoriesController(PlatformDbContext dbContext) : Controller
{
    public async Task<IActionResult> Index(string? keyword, bool? visible, CancellationToken cancellationToken)
    {
        var query = dbContext.Categories
            .AsNoTracking()
            .Include(x => x.Assets)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalizedKeyword = keyword.Trim();
            query = query.Where(x => x.Name.Contains(normalizedKeyword) || x.Slug.Contains(normalizedKeyword));
        }

        if (visible is not null)
        {
            query = query.Where(x => x.IsVisible == visible);
        }

        var items = await query
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new CategoryListItem(
                x.ID,
                x.Name,
                x.Slug,
                x.Description,
                x.SortOrder,
                x.IsVisible,
                x.Assets.Count))
            .ToListAsync(cancellationToken);

        var model = new CategoryIndexViewModel
        {
            Items = items,
            Keyword = keyword,
            Visible = visible,
            TotalCount = await dbContext.Categories.CountAsync(cancellationToken),
            VisibleCount = await dbContext.Categories.CountAsync(x => x.IsVisible, cancellationToken),
            HiddenCount = await dbContext.Categories.CountAsync(x => !x.IsVisible, cancellationToken)
        };

        return View(model);
    }

    public IActionResult Create()
    {
        return View(new CategoryEditViewModel { SortOrder = 50, IsVisible = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CategoryEditViewModel model, CancellationToken cancellationToken)
    {
        await ValidateSlugAsync(model, null, cancellationToken);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        dbContext.Categories.Add(new Category
        {
            Name = model.Name.Trim(),
            Slug = model.Slug.Trim(),
            Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
            SortOrder = model.SortOrder,
            IsVisible = model.IsVisible
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id, CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories.AsNoTracking().FirstOrDefaultAsync(x => x.ID == id, cancellationToken);
        if (category is null)
        {
            return NotFound();
        }

        var model = new CategoryEditViewModel
        {
            Id = category.ID,
            Name = category.Name,
            Slug = category.Slug,
            Description = category.Description,
            SortOrder = category.SortOrder,
            IsVisible = category.IsVisible
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, CategoryEditViewModel model, CancellationToken cancellationToken)
    {
        if (model.Id != id)
        {
            return BadRequest();
        }

        await ValidateSlugAsync(model, id, cancellationToken);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var category = await dbContext.Categories.FirstOrDefaultAsync(x => x.ID == id, cancellationToken);
        if (category is null)
        {
            return NotFound();
        }

        category.Name = model.Name.Trim();
        category.Slug = model.Slug.Trim();
        category.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        category.SortOrder = model.SortOrder;
        category.IsVisible = model.IsVisible;

        await dbContext.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleVisible(string id, CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories.FirstOrDefaultAsync(x => x.ID == id, cancellationToken);
        if (category is null)
        {
            return NotFound();
        }

        category.IsVisible = !category.IsVisible;
        await dbContext.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Batch(string[] ids, string operation, CancellationToken cancellationToken)
    {
        if (ids.Length == 0)
        {
            TempData["CategoryError"] = "请先选择要操作的分类。";
            return RedirectToAction(nameof(Index));
        }

        var categories = await dbContext.Categories
            .Include(x => x.Assets)
            .Where(x => ids.Contains(x.ID))
            .ToListAsync(cancellationToken);

        if (operation == "delete")
        {
            var deletable = categories.Where(x => x.Assets.Count == 0).ToList();
            dbContext.Categories.RemoveRange(deletable);
            await dbContext.SaveChangesAsync(cancellationToken);
            TempData["CategoryError"] = deletable.Count == categories.Count
                ? $"已批量删除 {deletable.Count} 个分类。"
                : $"已删除 {deletable.Count} 个空分类，存在资源的分类已跳过。";
            return RedirectToAction(nameof(Index));
        }

        foreach (var category in categories)
        {
            if (operation == "show")
            {
                category.IsVisible = true;
            }
            else if (operation == "hide")
            {
                category.IsVisible = false;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        TempData["CategoryError"] = $"已批量处理 {categories.Count} 个分类。";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories
            .Include(x => x.Assets)
            .FirstOrDefaultAsync(x => x.ID == id, cancellationToken);

        if (category is null)
        {
            return NotFound();
        }

        if (category.Assets.Count > 0)
        {
            TempData["CategoryError"] = "该分类下仍有资源，不能删除。请先调整资源分类。";
            return RedirectToAction(nameof(Index));
        }

        dbContext.Categories.Remove(category);
        await dbContext.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    private async Task ValidateSlugAsync(CategoryEditViewModel model, string? currentId, CancellationToken cancellationToken)
    {
        model.Name = model.Name.Trim();
        model.Slug = model.Slug.Trim();
        model.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();

        var exists = await dbContext.Categories
            .AnyAsync(x => x.Slug == model.Slug && x.ID != currentId, cancellationToken);

        if (exists)
        {
            ModelState.AddModelError(nameof(CategoryEditViewModel.Slug), "分类标识已存在");
        }
    }
}
