export function watchPagedTable(element, dotNetReference, options) {
  const settings = {
    minPageSize: Math.max(1, options?.minPageSize ?? 5),
    rowHeightPx: Math.max(1, options?.rowHeightPx ?? 58),
    viewportOffsetPx: Math.max(0, options?.viewportOffsetPx ?? 430)
  };

  let disposed = false;
  let pending = 0;

  const calculate = () => {
    if (disposed) {
      return;
    }

    const viewportHeight = window.innerHeight || document.documentElement.clientHeight || 0;
    const elementTop = element?.getBoundingClientRect?.().top ?? settings.viewportOffsetPx;
    const footerReserve = 96;
    const available = Math.max(0, viewportHeight - elementTop - footerReserve);
    const fallbackAvailable = Math.max(0, viewportHeight - settings.viewportOffsetPx);
    const usableHeight = available > 0 ? available : fallbackAvailable;
    const rows = Math.max(settings.minPageSize, Math.floor(usableHeight / settings.rowHeightPx));
    dotNetReference.invokeMethodAsync("SetViewportPageSize", rows);
  };

  const schedule = () => {
    if (pending !== 0) {
      window.clearTimeout(pending);
    }

    pending = window.setTimeout(calculate, 120);
  };

  window.addEventListener("resize", schedule);
  schedule();

  return {
    dispose() {
      disposed = true;
      if (pending !== 0) {
        window.clearTimeout(pending);
      }

      window.removeEventListener("resize", schedule);
    }
  };
}
