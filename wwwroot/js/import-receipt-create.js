(() => {
    "use strict";

    const form = document.getElementById("import-receipt-form");
    const body = document.getElementById("details-body");
    const template = document.getElementById("detail-row-template");
    const addButton = document.getElementById("add-detail-row");
    const totalElement = document.getElementById("grand-total");
    const clientError = document.getElementById("client-detail-error");
    const saveButton = document.getElementById("save-receipt");

    if (!form || !body || !template || !addButton || !totalElement || !clientError) {
        return;
    }

    const currencyFormatter = new Intl.NumberFormat("vi-VN", {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
    });
    const maximumQuantity = 999999999999999.999;
    const maximumUnitCost = 9999999999999999.99;

    function parseNumber(input) {
        const value = Number.parseFloat(input.value);
        return Number.isFinite(value) ? value : 0;
    }

    function reindexRows() {
        const rows = [...body.querySelectorAll(".detail-row")];
        rows.forEach((row, index) => {
            row.querySelector(".detail-number").textContent = String(index + 1);
            row.querySelectorAll("select, input").forEach(control => {
                control.name = control.name.replace(/Details\[\d+\]/, `Details[${index}]`);
                control.id = control.id.replace(/Details_\d+__/, `Details_${index}__`);
            });
            row.querySelectorAll("[data-valmsg-for]").forEach(message => {
                message.dataset.valmsgFor = message.dataset.valmsgFor.replace(
                    /Details\[\d+\]/,
                    `Details[${index}]`
                );
            });
        });
    }

    function updateTotals() {
        let total = 0;
        let hasInvalidLine = false;
        body.querySelectorAll(".detail-row").forEach(row => {
            const quantityInput = row.querySelector(".quantity-input");
            const unitCostInput = row.querySelector(".unit-cost-input");
            const quantity = parseNumber(quantityInput);
            const unitCost = parseNumber(unitCostInput);
            const lineTotal = quantity * unitCost;
            const hasValidPrecision =
                /^\d+(?:\.\d{1,3})?$/.test(quantityInput.value) &&
                /^\d+(?:\.\d{1,2})?$/.test(unitCostInput.value);
            if (hasValidPrecision && Number.isFinite(lineTotal)) {
                total += lineTotal;
                row.querySelector(".line-total").textContent = currencyFormatter.format(lineTotal);
            } else {
                hasInvalidLine = true;
                row.querySelector(".line-total").textContent = "Không hợp lệ";
            }
        });
        totalElement.textContent = !hasInvalidLine && Number.isFinite(total)
            ? currencyFormatter.format(total)
            : "Không hợp lệ";
    }

    function showClientError(message) {
        clientError.textContent = message;
        clientError.classList.remove("d-none");
    }

    function clearClientError() {
        clientError.textContent = "";
        clientError.classList.add("d-none");
    }

    function activateRowForEditing(row) {
        body.querySelectorAll(".detail-row").forEach(item => {
            item.classList.remove("table-primary", "editing-detail-row");
            const button = item.querySelector(".edit-detail-row");
            if (button) {
                button.textContent = "Sửa";
            }
        });

        row.classList.add("table-primary", "editing-detail-row");
        const editButton = row.querySelector(".edit-detail-row");
        if (editButton) {
            editButton.textContent = "Đang sửa";
        }
        row.querySelector(".product-select")?.focus();
    }

    addButton.addEventListener("click", () => {
        const index = body.querySelectorAll(".detail-row").length;
        const html = template.innerHTML.replaceAll("__index__", String(index));
        body.insertAdjacentHTML("beforeend", html);
        reindexRows();
        clearClientError();
        updateTotals();
    });

    body.addEventListener("click", event => {
        const editButton = event.target.closest(".edit-detail-row");
        if (editButton) {
            activateRowForEditing(editButton.closest(".detail-row"));
            return;
        }

        const removeButton = event.target.closest(".remove-detail-row");
        if (!removeButton) {
            return;
        }

        removeButton.closest(".detail-row").remove();
        reindexRows();
        updateTotals();
    });

    body.addEventListener("input", updateTotals);
    body.addEventListener("change", () => clearClientError());

    form.addEventListener("submit", event => {
        clearClientError();
        const rows = [...body.querySelectorAll(".detail-row")];
        if (rows.length === 0) {
            event.preventDefault();
            showClientError("Phiếu nhập phải có ít nhất một hàng hóa.");
            return;
        }

        const selectedProductIds = rows
            .map(row => row.querySelector(".product-select").value)
            .filter(value => value !== "0");
        if (new Set(selectedProductIds).size !== selectedProductIds.length) {
            event.preventDefault();
            showClientError("Hàng hóa không được trùng trong cùng một phiếu nhập.");
            return;
        }

        const hasInvalidDetail = rows.some(row => {
            const productId = Number.parseInt(row.querySelector(".product-select").value, 10);
            const quantityInput = row.querySelector(".quantity-input");
            const unitCostInput = row.querySelector(".unit-cost-input");
            const quantity = parseNumber(quantityInput);
            const unitCost = parseNumber(unitCostInput);
            return !Number.isInteger(productId) || productId <= 0 ||
                !/^\d+(?:\.\d{1,3})?$/.test(quantityInput.value) ||
                !/^\d+(?:\.\d{1,2})?$/.test(unitCostInput.value) ||
                quantity <= 0 || quantity > maximumQuantity ||
                unitCost < 0 || unitCost > maximumUnitCost;
        });
        if (hasInvalidDetail) {
            event.preventDefault();
            showClientError(
                "Vui lòng chọn hàng hóa, nhập số lượng dương (tối đa 3 chữ số thập phân) và đơn giá từ 0 (tối đa 2 chữ số thập phân)."
            );
            return;
        }

        reindexRows();
        if (saveButton) {
            saveButton.disabled = true;
            saveButton.textContent = "Đang lưu...";
        }
    });

    reindexRows();
    updateTotals();
})();
