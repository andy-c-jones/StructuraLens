/** Sortable table headers - click to toggle asc/desc. */
export function makeSortable(table: HTMLTableElement): void {
  const headers = table.querySelectorAll("th");
  const tbody = table.querySelector("tbody");
  if (!tbody) return;

  headers.forEach((header, index) => {
    header.addEventListener("click", () => {
      const rows = Array.from(tbody.querySelectorAll("tr"));
      const isAsc = header.classList.contains("sort-asc");

      headers.forEach((h) => h.classList.remove("sort-asc", "sort-desc"));
      header.classList.add(isAsc ? "sort-desc" : "sort-asc");

      rows.sort((a, b) => {
        const aVal = a.cells[index]?.textContent?.trim() ?? "";
        const bVal = b.cells[index]?.textContent?.trim() ?? "";

        const aNum = parseFloat(aVal.replace(/[^0-9.-]/g, ""));
        const bNum = parseFloat(bVal.replace(/[^0-9.-]/g, ""));

        if (!isNaN(aNum) && !isNaN(bNum)) {
          return isAsc ? bNum - aNum : aNum - bNum;
        }
        return isAsc ? bVal.localeCompare(aVal) : aVal.localeCompare(bVal);
      });

      rows.forEach((row) => tbody.appendChild(row));
    });
  });
}

/** Apply sorting to all tables in the document. */
export function enableSorting(): void {
  document.querySelectorAll<HTMLTableElement>("table").forEach(makeSortable);
}

/** Render pagination controls. Returns HTML string. */
export function renderPagination(
  currentPage: number,
  totalPages: number,
): string {
  if (totalPages <= 1) return "";

  const pages: (number | string)[] = [];
  const maxVisible = 7;

  if (totalPages <= maxVisible) {
    for (let i = 1; i <= totalPages; i++) pages.push(i);
  } else {
    if (currentPage <= 4) {
      for (let i = 1; i <= 5; i++) pages.push(i);
      pages.push("...");
      pages.push(totalPages);
    } else if (currentPage >= totalPages - 3) {
      pages.push(1);
      pages.push("...");
      for (let i = totalPages - 4; i <= totalPages; i++) pages.push(i);
    } else {
      pages.push(1);
      pages.push("...");
      for (let i = currentPage - 1; i <= currentPage + 1; i++) pages.push(i);
      pages.push("...");
      pages.push(totalPages);
    }
  }

  const buttons = pages
    .map((page) => {
      if (page === "...") {
        return '<span class="pagination-info">...</span>';
      }
      const isActive = page === currentPage;
      return `<button class="pagination-btn ${isActive ? "active" : ""}" data-page="${page}" ${isActive ? "disabled" : ""}>${page}</button>`;
    })
    .join("");

  return `
    <div class="pagination">
      <button class="pagination-btn" data-page="${currentPage - 1}" ${currentPage === 1 ? "disabled" : ""}>Previous</button>
      ${buttons}
      <button class="pagination-btn" data-page="${currentPage + 1}" ${currentPage === totalPages ? "disabled" : ""}>Next</button>
    </div>
  `;
}

/** Attach click listeners to pagination buttons inside a container. */
export function attachPaginationListeners(
  containerId: string,
  callback: (page: number) => void,
): void {
  const container = document.getElementById(containerId);
  if (!container) return;

  container
    .querySelectorAll<HTMLButtonElement>(".pagination-btn[data-page]")
    .forEach((btn) => {
      btn.addEventListener("click", () => {
        const page = parseInt(btn.dataset.page ?? "", 10);
        if (!isNaN(page)) callback(page);
      });
    });
}
