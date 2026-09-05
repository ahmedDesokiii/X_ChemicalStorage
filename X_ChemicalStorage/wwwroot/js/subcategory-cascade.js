// Category -> subCategory-cascading dropdown for Add/Edit Item.
// Same pattern as the existing Room -> StorageUnit -> Shelf -> Box cascade
// (see GetStorageUnitsByRoom / GetShelvesByStorageUnit / GetBoxesByShelf in Views/Items/Index.cshtml).

document.addEventListener('DOMContentLoaded', function () {
    const selCategory = document.getElementById('itemCategory');
    const selSubCategory = document.getElementById('itemSubCategory');

    if (!selCategory || !selSubCategory) return;

    function resetSubCategory(placeholder) {
        selSubCategory.innerHTML = `<option value="" selected disabled>${placeholder}</option>`;
        selSubCategory.disabled = true;
    }

    // Edit case: the server already rendered the real options (with the current
    // one marked Selected) via Model.ListSubCategories — just enable the select.
    // Add case: only the placeholder option exists — leave it disabled until
    // the user actually picks a Category.
    if (selSubCategory.options.length > 1) {
        selSubCategory.disabled = false;
    }

    selCategory.addEventListener('change', async () => {
        resetSubCategory('Loading...');

        const categoryId = selCategory.value;
        if (!categoryId) {
            resetSubCategory('Choose Category first');
            return;
        }

        try {
            const res = await fetch(`/SubCategories/GetSubCategoriesByCategory?categoryId=${categoryId}`);
            const subCategories = await res.json();

            resetSubCategory('Choose SubCategory');
            subCategories.forEach(sc => selSubCategory.add(new Option(sc.name, sc.id)));
            selSubCategory.disabled = false;
        } catch (err) {
            resetSubCategory('Failed to load, try again');
        }
    });
});
