from typing import Callable, Generic, NamedTuple, TypeVar
from mathutils import Vector

from io_scene_tr_reboot.util.Enumerable import Enumerable

T = TypeVar("T")
U = TypeVar("U")

class SpatialIndex(Generic[T]):
    class IndexItem(Generic[U], NamedTuple):
        position: Vector
        item: U

    class SearchState(Generic[U]):
        items: list[U]
        shell_size: int

        def __init__(self) -> None:
            self.items = []
            self.shell_size = -1

    grid: dict[tuple[int, int, int], list[IndexItem[T]]]
    cell_size: float

    grid_min_coords: list[int]
    grid_max_coords: list[int]

    def __init__(self, cell_size: float) -> None:
        self.grid = {}
        self.cell_size = cell_size

        self.grid_min_coords = [ 100000,  100000,  100000]
        self.grid_max_coords = [-100000, -100000, -100000]

    def add(self, position: Vector, item: T) -> None:
        cell_coords = self.__position_to_cell_coords(position)
        for i in range(3):
            self.grid_min_coords[i] = min(self.grid_min_coords[i], cell_coords[i])
            self.grid_max_coords[i] = max(self.grid_max_coords[i], cell_coords[i])

        items_in_cell = self.grid.get(cell_coords)
        if items_in_cell is None:
            items_in_cell = []
            self.grid[cell_coords] = items_in_cell

        items_in_cell.append(SpatialIndex.IndexItem(position, item))

    def find_nearest_item(self, position: Vector) -> T | None:
        _, state = self.find_nearby_items_mapped(position, bool, lambda items: len(items) > 0 and True or None)
        if len(state.items) > 0:
            return state.items[0]

        return None

    def find_nearby_items_mapped(self, position: Vector, result_type: type[U], get_result: Callable[[list[T]], U | None], state: SearchState[T] | None = None) -> tuple[U | None, SearchState[T]]:
        if state is None:
            state = SpatialIndex.SearchState()
        else:
            result = get_result(state.items)
            if result is not None:
                return (result, state)

        center_coords = self.__position_to_cell_coords(position)

        while True:
            state.shell_size += 1
            shell_min_coords = (
                center_coords[0] - state.shell_size,
                center_coords[1] - state.shell_size,
                center_coords[2] - state.shell_size
            )
            truncated_shell_min_coords = (
                max(shell_min_coords[0], self.grid_min_coords[0]),
                max(shell_min_coords[1], self.grid_min_coords[1]),
                max(shell_min_coords[2], self.grid_min_coords[2])
            )
            shell_max_coords = (
                center_coords[0] + state.shell_size,
                center_coords[1] + state.shell_size,
                center_coords[2] + state.shell_size
            )
            truncated_shell_max_coords = (
                min(shell_max_coords[0], self.grid_max_coords[0]),
                min(shell_max_coords[1], self.grid_max_coords[1]),
                min(shell_max_coords[2], self.grid_max_coords[2])
            )
            items_in_shell: list[SpatialIndex.IndexItem[T]] = []
            for cell_x in range(truncated_shell_min_coords[0], truncated_shell_max_coords[0] + 1):
                for cell_y in range(truncated_shell_min_coords[1], truncated_shell_max_coords[1] + 1):
                    for cell_z in range(truncated_shell_min_coords[2], truncated_shell_max_coords[2] + 1):
                        if cell_x > shell_min_coords[0] and cell_x < shell_max_coords[0] and \
                           cell_y > shell_min_coords[1] and cell_y < shell_max_coords[1] and \
                           cell_z > shell_min_coords[2] and cell_z < shell_max_coords[2]:
                            continue

                        items_in_cell = self.grid.get((cell_x, cell_y, cell_z))
                        if items_in_cell is None:
                            continue

                        items_in_shell.extend(items_in_cell)

            items_in_shell.sort(key = lambda item: (item.position - position).length_squared)
            state.items.extend(Enumerable(items_in_shell).select(lambda i: i.item))

            result = get_result(state.items)
            if result is not None:
                return (result, state)

            if truncated_shell_min_coords == tuple(self.grid_min_coords) and truncated_shell_max_coords == tuple(self.grid_max_coords):
                break

        return (None, state)

    def __position_to_cell_coords(self, position: Vector) -> tuple[int, int, int]:
        return (
            int(round(position[0] / self.cell_size)),
            int(round(position[1] / self.cell_size)),
            int(round(position[2] / self.cell_size))
        )
