"""
gui/plot_widgets.py

Виджеты matplotlib, встроенные в PyQt6 через FigureCanvasQTAgg.

- Plot2DCanvas: scatter эмпирических точек + линия аппроксимации + MSE
  в легенде. Используется для Частей I и III (время) и Части IV (шаги).
  Также умеет рисовать НЕСКОЛЬКО серий сразу - для режима "Сравнить
  алгоритмы".
- Plot3DCanvas: поверхность (surface) время(n, m) - для Части II.
"""

from __future__ import annotations

import numpy as np
from matplotlib.backends.backend_qtagg import FigureCanvasQTAgg
from matplotlib.figure import Figure

from core.approximation import ApproximationResult

# Устойчивый набор цветов для нескольких серий на одном графике
# (режим сравнения алгоритмов) - matplotlib default cycle тоже подошёл бы,
# но явный список гарантирует воспроизводимость между запусками.
_SERIES_COLORS = [
    "#1f77b4", "#d62728", "#2ca02c", "#9467bd",
    "#ff7f0e", "#8c564b", "#17becf", "#e377c2",
]


class Plot2DCanvas(FigureCanvasQTAgg):
    """2D-график: эмпирические точки (scatter) + кривая аппроксимации (line)."""

    def __init__(self, parent=None, width: float = 7.0, height: float = 5.0, dpi: int = 100):
        self.figure = Figure(figsize=(width, height), dpi=dpi)
        self.axes = self.figure.add_subplot(111)
        super().__init__(self.figure)
        self.setParent(parent)
        self._draw_placeholder()

    def _draw_placeholder(self) -> None:
        self.axes.clear()
        self.axes.set_title("Запустите эксперимент, чтобы увидеть график")
        self.axes.set_xlabel("n")
        self.axes.set_ylabel("значение метрики")
        self.draw()

    def plot_single(
        self,
        n_values: list[float],
        metric_values: list[float],
        metric_label: str,
        title: str,
        fit: ApproximationResult | None = None,
    ) -> None:
        """
        Рисует одну серию: точки эмпирических замеров + (опционально)
        сглаженную кривую аппроксимации C*f(n) с MSE в легенде.
        """
        self.axes.clear()

        n_arr = np.asarray(n_values, dtype=float)
        y_arr = np.asarray(metric_values, dtype=float)

        self.axes.scatter(
            n_arr, y_arr, color=_SERIES_COLORS[0], s=28, zorder=3,
            label="Эмпирические данные",
        )

        if fit is not None:
            # Строим кривую на плотной сетке n для гладкости, а не только
            # по фактическим точкам замера.
            n_smooth = np.linspace(n_arr.min(), n_arr.max(), 300)
            from core.approximation import MODEL_FUNCTIONS
            f = MODEL_FUNCTIONS[fit.model_name]
            y_smooth = fit.C * f(n_smooth)
            self.axes.plot(
                n_smooth, y_smooth, color=_SERIES_COLORS[1], linewidth=2, zorder=2,
                label=f"Аппроксимация: {fit.C:.4g}·{fit.model_name}  (MSE={fit.mse:.4g})",
            )

        self.axes.set_title(title)
        self.axes.set_xlabel("n")
        self.axes.set_ylabel(metric_label)
        self.axes.legend(loc="upper left", fontsize=9)
        self.axes.grid(True, linestyle="--", alpha=0.4)
        self.figure.tight_layout()
        self.draw()

    def plot_multiple(
        self,
        series: dict[str, tuple[list[float], list[float]]],
        metric_label: str,
        title: str,
    ) -> None:
        """
        Рисует несколько серий на одном графике (без аппроксимации) - для
        режима "Сравнить алгоритмы".

        Parameters
        ----------
        series : dict[str, tuple[list[float], list[float]]]
            {отображаемое_имя_алгоритма: (n_values, metric_values)}
        """
        self.axes.clear()

        for i, (name, (n_values, y_values)) in enumerate(series.items()):
            color = _SERIES_COLORS[i % len(_SERIES_COLORS)]
            n_arr = np.asarray(n_values, dtype=float)
            y_arr = np.asarray(y_values, dtype=float)
            order = np.argsort(n_arr)
            self.axes.plot(
                n_arr[order], y_arr[order],
                marker="o", markersize=4, linewidth=1.5,
                color=color, label=name,
            )

        self.axes.set_title(title)
        self.axes.set_xlabel("n")
        self.axes.set_ylabel(metric_label)
        self.axes.legend(loc="upper left", fontsize=9)
        self.axes.grid(True, linestyle="--", alpha=0.4)
        self.figure.tight_layout()
        self.draw()

    def clear(self) -> None:
        self._draw_placeholder()


class Plot3DCanvas(FigureCanvasQTAgg):
    """3D-поверхность: зависимость времени от (n, m). Используется для Части II."""

    def __init__(self, parent=None, width: float = 7.0, height: float = 5.5, dpi: int = 100):
        self.figure = Figure(figsize=(width, height), dpi=dpi)
        self.axes = self.figure.add_subplot(111, projection="3d")
        super().__init__(self.figure)
        self.setParent(parent)
        self._draw_placeholder()

    def _draw_placeholder(self) -> None:
        self.axes.clear()
        self.axes.set_title("Запустите эксперимент для умножения матриц")
        self.axes.set_xlabel("n")
        self.axes.set_ylabel("m")
        self.axes.set_zlabel("время, с")
        self.draw()

    def plot_surface(
        self,
        n_values: list[int],
        m_values: list[int],
        time_grid: list[list[float]],
        title: str = "Умножение матриц: время от n и m",
    ) -> None:
        """
        Рисует поверхность time_grid[i][j] = время для (n_values[i], m_values[j]).
        """
        self.axes.clear()

        n_arr = np.asarray(n_values, dtype=float)
        m_arr = np.asarray(m_values, dtype=float)
        z_arr = np.asarray(time_grid, dtype=float)  # shape (len(n), len(m))

        # meshgrid с indexing='ij' согласуется с тем, как построен time_grid:
        # ось 0 - n, ось 1 - m.
        N, M = np.meshgrid(n_arr, m_arr, indexing="ij")

        surf = self.axes.plot_surface(
            N, M, z_arr, cmap="viridis", edgecolor="none", alpha=0.9,
        )
        self.figure.colorbar(surf, ax=self.axes, shrink=0.6, label="время, с")

        self.axes.set_title(title)
        self.axes.set_xlabel("n")
        self.axes.set_ylabel("m")
        self.axes.set_zlabel("время, с")
        # tight_layout() плохо уживается с 3D-осями + colorbar (даёт
        # UserWarning и не улучшает раскладку) - используем ручные отступы.
        self.figure.subplots_adjust(left=0.05, right=0.95, top=0.95, bottom=0.05)
        self.draw()

    def clear(self) -> None:
        self._draw_placeholder()
