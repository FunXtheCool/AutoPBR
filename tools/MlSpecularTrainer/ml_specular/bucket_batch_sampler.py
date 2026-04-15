"""Batch sampler that groups dataset indices by identical (train_w, train_h) for native spatial training."""

from __future__ import annotations

import math
from collections import defaultdict
from typing import Iterator

import torch
from torch.utils.data import Sampler


class BucketBatchSampler(Sampler[list[int]]):
    """
    Yields batches of indices that share the same training width/height (after cap), so the default
    collate stacks tensors without padding. Each index appears once per epoch.

    For training, shuffle=True shuffles batch order and index order within each bucket.
    """

    def __init__(
        self,
        keys: list[tuple[int, int]],
        batch_size: int,
        *,
        shuffle: bool,
        generator: torch.Generator | None = None,
    ) -> None:
        if len(keys) == 0:
            raise ValueError("BucketBatchSampler: empty keys")
        if batch_size < 1:
            raise ValueError("batch_size must be >= 1")
        self._keys = keys
        self._batch_size = batch_size
        self._shuffle = shuffle
        self._generator = generator

        bucket_to_indices: dict[tuple[int, int], list[int]] = defaultdict(list)
        for i, k in enumerate(keys):
            bucket_to_indices[k].append(i)
        # Deterministic bucket order when shuffle=False: sort by (w, h)
        self._buckets = sorted(bucket_to_indices.items(), key=lambda kv: (kv[0][0], kv[0][1]))

    def __len__(self) -> int:
        n = 0
        for _, idxs in self._buckets:
            n += int(math.ceil(len(idxs) / self._batch_size))
        return n

    def __iter__(self) -> Iterator[list[int]]:
        g = self._generator
        bucket_order = list(range(len(self._buckets)))
        if self._shuffle:
            if g is not None:
                perm = torch.randperm(len(bucket_order), generator=g).tolist()
                bucket_order = [bucket_order[i] for i in perm]
            else:
                perm = torch.randperm(len(bucket_order)).tolist()
                bucket_order = [bucket_order[i] for i in perm]

        for bi in bucket_order:
            _, idxs = self._buckets[bi]
            if self._shuffle:
                order = list(idxs)
                if g is not None:
                    perm_i = torch.randperm(len(order), generator=g).tolist()
                    order = [order[j] for j in perm_i]
                else:
                    perm_i = torch.randperm(len(order)).tolist()
                    order = [order[j] for j in perm_i]
            else:
                order = sorted(idxs)

            for s in range(0, len(order), self._batch_size):
                yield order[s : s + self._batch_size]
