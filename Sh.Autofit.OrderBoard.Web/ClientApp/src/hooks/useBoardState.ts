import { useState, useCallback, useRef } from 'react';
import type { BoardResponse } from '../types';
import { getBoard } from '../api/client';

export function useBoardState() {
  const [board, setBoard] = useState<BoardResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [includeArchived, setIncludeArchived] = useState(false);
  const fetchingRef = useRef(false);

  const refresh = useCallback(async () => {
    // Debounce: skip if already fetching
    if (fetchingRef.current) return;
    fetchingRef.current = true;

    try {
      const data = await getBoard(includeArchived);
      setBoard(data);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load board');
      console.error('Board fetch error:', err);
    } finally {
      setLoading(false);
      fetchingRef.current = false;
    }
  }, [includeArchived]);

  const toggleArchived = useCallback(() => {
    setIncludeArchived((prev) => !prev);
  }, []);

  return { board, loading, error, includeArchived, refresh, toggleArchived };
}
