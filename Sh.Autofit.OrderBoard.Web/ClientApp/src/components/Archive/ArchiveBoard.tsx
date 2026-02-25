import { useMemo } from 'react';
import type { ArchiveBoardResponse, BoardColumn, OrderCard } from '../../types';
import ArchiveColumn from './ArchiveColumn';

interface Props {
  board: ArchiveBoardResponse;
  searchQuery: string;
  onShowTimeline: (appOrderId: number) => void;
}

function matchesSearch(order: OrderCard, q: string): boolean {
  const lower = q.toLowerCase();
  return (
    order.accountKey.toLowerCase().includes(lower) ||
    (order.accountName ?? '').toLowerCase().includes(lower) ||
    (order.city ?? '').toLowerCase().includes(lower) ||
    (order.address ?? '').toLowerCase().includes(lower)
  );
}

export default function ArchiveBoard({ board, searchQuery, onShowTimeline }: Props) {
  const filteredColumns = useMemo(() => {
    if (!searchQuery.trim()) return board.columns;

    return board.columns.map((col): BoardColumn => {
      const filteredGroups = col.groups.map((g) => {
        const filtered = g.orders.filter((o) => matchesSearch(o, searchQuery));
        return { ...g, orders: filtered, count: filtered.length };
      });
      const totalCount = filteredGroups.reduce((sum, g) => sum + g.count, 0);
      return { ...col, groups: filteredGroups, count: totalCount };
    });
  }, [board.columns, searchQuery]);

  return (
    <div className="flex gap-3 p-4 pb-8 overflow-x-auto" dir="rtl">
      {filteredColumns.map((col) => (
        <ArchiveColumn
          key={col.stage}
          column={col}
          onShowTimeline={onShowTimeline}
        />
      ))}
    </div>
  );
}
