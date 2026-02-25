import { useState, useMemo } from 'react';
import type { OrderCard } from '../types';
import type { SortOption } from '../components/KanbanBoard/ColumnSortFilter';

export function useColumnSortFilter(orders: OrderCard[]) {
  const [sortOption, setSortOption] = useState<SortOption>('default');
  const [filterMethodId, setFilterMethodId] = useState<number | null>(null);

  const processedOrders = useMemo(() => {
    let result = [...orders];

    // Filter by delivery method
    if (filterMethodId !== null) {
      result = result.filter((o) => o.deliveryMethodId === filterMethodId);
    }

    // Sort
    switch (sortOption) {
      case 'newest':
        result.sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());
        break;
      case 'oldest':
        result.sort((a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime());
        break;
      case 'accountName':
        result.sort((a, b) => (a.accountName ?? '').localeCompare(b.accountName ?? '', 'he'));
        break;
      case 'city':
        result.sort((a, b) => (a.city ?? '').localeCompare(b.city ?? '', 'he'));
        break;
      default:
        break;
    }

    return result;
  }, [orders, sortOption, filterMethodId]);

  return { processedOrders, sortOption, setSortOption, filterMethodId, setFilterMethodId };
}
