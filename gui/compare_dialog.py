"""
gui/compare_dialog.py

Диалог "Сравнить алгоритмы": показывает список алгоритмов, по которым в
БД уже накоплена хоть какая-то история замеров, и позволяет отметить
несколько галочками.
"""

from __future__ import annotations

from PyQt6.QtWidgets import (
    QDialog, QVBoxLayout, QListWidget, QListWidgetItem,
    QDialogButtonBox, QLabel,
)
from PyQt6.QtCore import Qt

from core.benchmark import ALGORITHM_REGISTRY


class CompareAlgorithmsDialog(QDialog):
    """
    Modal-диалог со списком алгоритмов (чекбоксы). Список наполняется
    только теми ключами, для которых реально есть данные в БД
    (available_keys), чтобы не предлагать сравнивать то, что ни разу не
    запускалось.
    """

    def __init__(self, available_keys: list[str], parent=None):
        super().__init__(parent)
        self.setWindowTitle("Сравнить алгоритмы")
        self.resize(420, 400)

        layout = QVBoxLayout(self)

        if not available_keys:
            layout.addWidget(QLabel(
                "В базе данных пока нет ни одного результата.\n"
                "Сначала запустите хотя бы один эксперимент."
            ))
        else:
            layout.addWidget(QLabel("Отметьте алгоритмы для сравнения (2 и более):"))

        self.list_widget = QListWidget()
        for key in available_keys:
            spec = ALGORITHM_REGISTRY.get(key)
            label = spec.display_name if spec is not None else key
            item = QListWidgetItem(label)
            item.setData(Qt.ItemDataRole.UserRole, key)
            item.setFlags(item.flags() | Qt.ItemFlag.ItemIsUserCheckable)
            item.setCheckState(Qt.CheckState.Unchecked)
            self.list_widget.addItem(item)
        layout.addWidget(self.list_widget)

        buttons = QDialogButtonBox(
            QDialogButtonBox.StandardButton.Ok | QDialogButtonBox.StandardButton.Cancel
        )
        buttons.accepted.connect(self._on_accept)
        buttons.rejected.connect(self.reject)
        layout.addWidget(buttons)

        self._selected_keys: list[str] = []

    def _on_accept(self) -> None:
        self._selected_keys = [
            self.list_widget.item(i).data(Qt.ItemDataRole.UserRole)
            for i in range(self.list_widget.count())
            if self.list_widget.item(i).checkState() == Qt.CheckState.Checked
        ]
        if len(self._selected_keys) < 1:
            # Разрешаем закрыть без выбора - просто ничего не произойдёт,
            # отдельного предупреждения не делаем, чтобы не мешать пользователю.
            self.reject()
            return
        self.accept()

    def selected_keys(self) -> list[str]:
        return self._selected_keys
