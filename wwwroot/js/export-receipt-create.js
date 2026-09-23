(() => {
    "use strict";

    const form = document.getElementById("export-receipt-form");
    const body = document.getElementById("export-details-body");
    const template = document.getElementById("export-detail-row-template");
    const addButton = document.getElementById("add-export-detail-row");
    const clientError = document.getElementById("export-client-error");
    const saveButton = document.getElementById("save-export-receipt");

    if (!form || !body || !template || !addButton || !clientError) {
        return;
    }

    function reindexRows() {
        [...body.querySelectorAll(".detail-row")].forEach((row, index) => {
            row.querySelector(".detail-number").textContent = String(index + 1);
            row.querySelectorAll("select, input").forEach(control => {
                control.name = control.name.replace(/Details\[\d+\]/, `Details[${index}]`);
                control.id = control.id.replace(/Details_\d+__/, `Details_${index}__`);
            });
            row.querySelectorAll("[data-valmsg-for]").forEach(message => {
                message.dataset.valmsgFor = message.dataset.valmsgFor.replace(
                    /Details\[\d+\]/, `Details[${index}]`);
            });
        });
    }

    function selectedStock(row) {
        const selected = row.querySelector(".product-select").selectedOptions[0];
        const stock = Number(selected?.dataset.stock);
        return Number.isFinite(stock) && stock > 0 ? stock : 0;
    }

    function updateStockCells() {
        body.querySelectorAll(".detail-row").forEach(row => {
            const stock = selectedStock(row);
            row.querySelector(".stock-cell").textContent = stock > 0
                ? `${stock.toLocaleString("vi-VN", { maximumFractionDigits: 3 })}`
                : "—";
        });
    }

    function showClientError(message) {
        clientError.textContent = message;
        clientError.classList.remove("d-none");
    }

    function clearClientError() {
        clientError.textContent = "";
        clientError.classList.add("d-none");
    }

    addButton.addEventListener("click", () => {
        const index = body.querySelectorAll(".detail-row").length;
        body.insertAdjacentHTML("beforeend", template.innerHTML.replaceAll("__index__", String(index)));
        reindexRows();
        updateStockCells();
        clearClientError();
    });

    body.addEventListener("click", event => {
        const editButton = event.target.closest(".edit-detail-row");
        if (editButton) {
            body.querySelectorAll(".detail-row").forEach(row => {
                row.classList.remove("table-primary", "editing-detail-row");
                row.querySelector(".edit-detail-row").textContent = "Sửa";
            });
            const row = editButton.closest(".detail-row");
            row.classList.add("table-primary", "editing-detail-row");
            editButton.textContent = "Đang sửa";
            row.querySelector(".product-select").focus();
            return;
        }

        const removeButton = event.target.closest(".remove-detail-row");
        if (removeButton) {
            removeButton.closest(".detail-row").remove();
            reindexRows();
            updateStockCells();
            clearClientError();
        }
    });

    body.addEventListener("change", () => {
        updateStockCells();
        clearClientError();
    });

    form.addEventListener("submit", event => {
        clearClientError();
        const rows = [...body.querySelectorAll(".detail-row")];
        if (rows.length === 0) {
            event.preventDefault();
            showClientError("Phiếu xuất phải có ít nhất một hàng hóa.");
            return;
        }

        const productIds = rows.map(row => row.querySelector(".product-select").value);
        if (productIds.includes("0")) {
            event.preventDefault();
            showClientError("Vui lòng chọn hàng hóa cho tất cả các dòng.");
            return;
        }
        if (new Set(productIds).size !== productIds.length) {
            event.preventDefault();
            showClientError("Hàng hóa không được trùng trong cùng một phiếu xuất.");
            return;
        }

        for (const row of rows) {
            const quantityText = row.querySelector(".quantity-input").value;
            const quantity = Number(quantityText);
            const stock = selectedStock(row);
            if (!/^\d+(?:\.\d{1,3})?$/.test(quantityText) ||
                !Number.isFinite(quantity) || quantity <= 0 || quantity > stock) {
                event.preventDefault();
                showClientError("Số lượng xuất phải lớn hơn 0, có tối đa 3 chữ số thập phân và không vượt tồn hiện tại.");
                return;
            }
        }

        reindexRows();
        if (saveButton) {
            saveButton.disabled = true;
            saveButton.textContent = "Đang lưu...";
        }
    });

    reindexRows();
    updateStockCells();
})();
