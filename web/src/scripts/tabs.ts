/** Tab switching logic. */
export function initTabs(onTabChange?: (tabId: string) => void): void {
  document.querySelectorAll<HTMLElement>(".tab").forEach((tab) => {
    tab.addEventListener("click", () => {
      document
        .querySelectorAll(".tab")
        .forEach((t) => t.classList.remove("active"));
      document
        .querySelectorAll(".tab-content")
        .forEach((c) => c.classList.remove("active"));
      tab.classList.add("active");

      const tabId = tab.dataset.tab;
      if (tabId) {
        document.getElementById(tabId)?.classList.add("active");
        onTabChange?.(tabId);
      }
    });
  });
}
