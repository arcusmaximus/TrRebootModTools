import inspect
import itertools
from typing import Any, Callable, Generic, Iterable, Iterator, Literal, TypeVar, cast, overload

T = TypeVar("T")
U = TypeVar("U")
V = TypeVar("V")

class Enumerable(Generic[T]):
    iterable: Iterable[T]

    def __init__(self, iterable: Iterable[T]) -> None:
        self.iterable = iterable

    def __iter__(self) -> Iterator[T]:
        return iter(self.iterable)

    def any(self, predicate: Callable[[T], bool] | None = None) -> bool:
        if predicate is None:
            try:
                next(iter(self))
                return True
            except StopIteration:
                return False
        else:
            return any(map(predicate, self))

    def all(self, predicate: Callable[[T], bool]) -> bool:
        return all(map(predicate, self))

    def contains(self, to_search: T) -> bool:
        for item in self:
            if item == to_search:
                return True

        return False

    def skip(self, skip_count: int) -> "Enumerable[T]":
        return _SkipEnumerable[T](self, skip_count)

    def take(self, take_count: int) -> "Enumerable[T]":
        return _TakeEnumerable[T](self, take_count)

    def where(self, predicate: Callable[[T], bool]) -> "Enumerable[T]":
        return _WhereEnumerable[T](self, predicate)

    def concat(self, next_iterable: Iterable[T]) -> "Enumerable[T]":
        return _ConcatEnumerable(self, next_iterable)

    def union(self, with_iterable: Iterable[T]) -> "Enumerable[T]":
        result = set(self)
        result.update(with_iterable)
        return Enumerable[T](result)

    def intersect(self, with_iterable: Iterable[T]) -> "Enumerable[T]":
        result = set(self)
        result.intersection_update(with_iterable)
        return Enumerable[T](result)

    def order_by(self, key_selector: Callable[[T], Any]) -> "Enumerable[T]":
        return _OrderByEnumerable[T](self, key_selector, False)

    def order_by_descending(self, key_selector: Callable[[T], Any]) -> "Enumerable[T]":
        return _OrderByEnumerable[T](self, key_selector, True)

    def select(self, mapping: Callable[[T], U]) -> "Enumerable[U]":
        return _SelectEnumerable[U](self, mapping)

    def select_many(self, mapping: Callable[[T], Iterable[U]]) -> "Enumerable[U]":
        return _SelectManyEnumerable[U](self, mapping)

    @overload
    def zip(self, other: Iterable[U]) -> "Enumerable[tuple[T, U]]": ...

    @overload
    def zip(self, other: Iterable[U], mapping: Callable[[T, U], V]) -> "Enumerable[V]": ...

    def zip(self, other: Iterable[Any], mapping: Callable[[Any, Any], Any] | None = None) -> Any:
        return _ZipEnumerable[Any](self, other, mapping)

    def distinct(self) -> "Enumerable[T]":
        return Enumerable[T](set(self))

    def of_type(self, t: type[U]) -> "Enumerable[U]":
        return self.where(lambda item: isinstance(item, t)).cast(t)

    def cast(self, t: type[U]) -> "Enumerable[U]":
        return cast(Enumerable[U], self)

    def first_or_none(self, predicate: Callable[[T], bool] | None = None) -> T | None:
        for item in self:
            if predicate is None or predicate(item):
                return item

        return None

    def first(self, predicate: Callable[[T], bool] | None = None) -> T:
        item = self.first_or_none(predicate)
        if item is None:
            raise Exception("No items in sequence")

        return item

    def last_or_none(self, predicate: Callable[[T], bool] | None = None) -> T | None:
        last_item: T | None = None
        for item in self:
            if predicate is None or predicate(item):
                last_item = item

        return last_item

    def last(self, predicate: Callable[[T], bool] | None = None) -> T:
        item = self.last_or_none(predicate)
        if item is None:
            raise Exception("No items in sequence")

        return item

    @overload
    def min(self, *, default_value: T | None = None) -> T: ...

    @overload
    def min(self, mapping: Callable[[T], U], *, default_value: T | None = None) -> U: ...

    def min(self, mapping = None, *, default_value = None):     # type: ignore
        try:
            if mapping is None:
                return min(self)                                # type: ignore
            else:
                return min(map(mapping, self))                  # type: ignore
        except ValueError:
            if default_value is not None:
                return default_value                            # type: ignore

            raise

    @overload
    def max(self, *, default_value: T | None = None) -> T: ...

    @overload
    def max(self, mapping: Callable[[T], U], *, default_value: U | None = None) -> U: ...

    def max(self, mapping = None, *, default_value = None):     # type: ignore
        try:
            if mapping is None:
                return max(self)                                # type: ignore
            else:
                return max(map(mapping, self))                  # type: ignore
        except ValueError:
            if default_value is not None:
                return default_value                            # type: ignore

            raise

    @overload
    def sum(self) -> T | Literal[0]: ...

    @overload
    def sum(self, mapping: Callable[[T], U]) -> U | Literal[0]: ...

    def sum(self, mapping = None):              # type: ignore
        if mapping is None:
            return sum(self)                    # type: ignore
        else:
            return sum(map(mapping, self))      # type: ignore

    @overload
    def avg(self) -> T: ...

    @overload
    def avg(self, mapping: Callable[[T], U]) -> U: ...

    def avg(self, mapping: Any = None) -> Any:
        sum = None
        count = 0
        for item in self:
            if mapping is not None:
                item = mapping(item)

            if count == 0:
                sum = item
            else:
                sum = sum + item                    # type: ignore

            count += 1

        if count == 0:
            raise Exception("Can't calculate average of empty sequence")

        return sum / count                          # type: ignore

    def count(self, predicate: Callable[[T], bool] | None = None) -> int:
        result = 0
        for item in self:
            if not predicate or predicate(item):
                result += 1

        return result

    def index_of(self, item_or_predicate: T | Callable[[T], bool]) -> int:
        index = 0
        if inspect.isfunction(item_or_predicate):
            for item in self:
                if item_or_predicate(item):
                    return index

                index += 1
        else:
            for item in self:
                if item == item_or_predicate:
                    return index

                index += 1

        return -1

    def to_tuple(self) -> tuple[T, ...]:
        return tuple(self)

    def to_list(self) -> list[T]:
        return list(self)

    def to_set(self) -> set[T]:
        return set(self)

    @overload
    def to_dict(self, key_selector: Callable[[T], U]) -> dict[U, T]: ...

    @overload
    def to_dict(self, key_selector: Callable[[T], U], value_selector: Callable[[T], V]) -> dict[U, V]: ...

    def to_dict(self, key_selector: Callable, value_selector: Callable | None = None) -> dict:  # type: ignore
        if value_selector is not None:
            return { key_selector(item): value_selector(item) for item in self }                # type: ignore
        else:
            return { key_selector(item): item for item in self }                                # type: ignore

    def group_by(self, key_selector: Callable[[T], U]) -> dict[U, list[T]]:
        lookup: dict[U, list[T]] = {}
        for item in self:
            key = key_selector(item)
            items_of_key = lookup.get(key)
            if items_of_key is None:
                items_of_key = []
                lookup[key] = items_of_key

            items_of_key.append(item)

        return lookup

    def with_descendants(self, get_children: Callable[[T], Iterable[T]]) -> "Enumerable[T]":
        return _WithDescendantsEnumerable(self, get_children)

class _SkipEnumerable(Enumerable[T]):
    skip_count: int

    def __init__(self, iterable: Iterable[T], skip_count: int) -> None:
        super().__init__(iterable)
        self.skip_count = skip_count

    def __iter__(self) -> Iterator[T]:
        return itertools.islice(self.iterable, self.skip_count, None)

class _TakeEnumerable(Enumerable[T]):
    take_count: int

    def __init__(self, iterable: Iterable[T], take_count: int) -> None:
        super().__init__(iterable)
        self.take_count = take_count

    def __iter__(self) -> Iterator[T]:
        return itertools.islice(self.iterable, None, self.take_count)

class _WhereEnumerable(Enumerable[T]):
    predicate: Callable[[T], bool]

    def __init__(self, iterable: Iterable[T], predicate: Callable[[T], bool]) -> None:
        super().__init__(iterable)
        self.predicate = predicate

    def __iter__(self) -> Iterator[T]:
        return filter(self.predicate, self.iterable)

class _ConcatEnumerable(Enumerable[T]):
    next_iterable: Iterable[T]

    def __init__(self, iterable: Iterable[T], next_iterable: Iterable[T]) -> None:
        super().__init__(iterable)
        self.next_iterable = next_iterable

    def __iter__(self) -> Iterator[T]:
        return itertools.chain(self.iterable, self.next_iterable)

class _OrderByEnumerable(Enumerable[T]):
    key_selector: Callable[[Any], Any]
    descending: bool

    def __init__(self, iterable: Iterable[Any], key_selector: Callable[[Any], Any], descending: bool) -> None:
        super().__init__(iterable)
        self.key_selector = key_selector
        self.descending = descending

    def __iter__(self) -> Iterator[T]:
        return iter(sorted(self.iterable, key = self.key_selector, reverse = self.descending))

class _SelectEnumerable(Enumerable[T]):
    mapping: Callable[[Any], Any]

    def __init__(self, iterable: Iterable[Any], mapping: Callable[[Any], Any]) -> None:
        super().__init__(iterable)
        self.mapping = mapping

    def __iter__(self) -> Iterator[T]:
        return map(self.mapping, self.iterable)

class _SelectManyEnumerable(Enumerable[T]):
    mapping: Callable[[Any], Iterable[Any]]

    def __init__(self, iterable: Iterable[Any], mapping: Callable[[Any], Iterable[Any]]) -> None:
        super().__init__(iterable)
        self.mapping = mapping

    def __iter__(self) -> Iterator[T]:
        return itertools.chain.from_iterable(map(self.mapping, self.iterable))

class _ZipEnumerable(Enumerable[T]):
    iterable2: Iterable[Any]
    mapping: Callable[[Any, Any], Any] | None

    def __init__(self, iterable: Iterable[Any], iterable2: Iterable[Any], mapping: Callable[[Any, Any], Any] | None) -> None:
        super().__init__(iterable)
        self.iterable2 = iterable2
        self.mapping = mapping

    def __iter__(self) -> Iterator[T]:
        result: Iterator[Any] = zip(self.iterable, self.iterable2)
        mapping = self.mapping
        if mapping is not None:
            result = map(lambda p: mapping(p[0], p[1]), result)

        return result

class _WithDescendantsEnumerable(Enumerable[T]):
    get_children: Callable[[T], Iterable[T]]
    queue: list[Iterator[T]]

    def __init__(self, root_items: Iterable[T], get_children: Callable[[T], Iterable[T]]) -> None:
        super().__init__(root_items)
        self.get_children = get_children
        self.queue = [iter(root_items)]

    def __iter__(self) -> Iterator[T]:
        while len(self.queue) > 0:
            it = self.queue[len(self.queue) - 1]
            try:
                item = next(it)
                yield item
                self.queue.append(iter(self.get_children(item)))
            except StopIteration:
                self.queue.pop()
