import { useState, useEffect, useRef } from 'react';
import { searchParts } from '../api/client';
import { useDebounce } from '../hooks/useDebounce';
import type { PartInfo } from '../types';

interface Props {
  onSelect: (part: PartInfo) => void;
  initialValue?: string;
}

export default function ItemSearch({ onSelect, initialValue }: Props) {
  const [query, setQuery] = useState(initialValue ?? '');
  const [results, setResults] = useState<PartInfo[]>([]);
  const [isOpen, setIsOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const debouncedQuery = useDebounce(query, 300);
  const containerRef = useRef<HTMLDivElement>(null);
  const suppressSearch = useRef(false);

  useEffect(() => {
    if (suppressSearch.current) {
      suppressSearch.current = false;
      return;
    }
    if (debouncedQuery.length < 2) {
      setResults([]);
      setIsOpen(false);
      return;
    }
    setLoading(true);
    searchParts(debouncedQuery)
      .then(r => {
        setResults(r.slice(0, 10));
        setIsOpen(r.length > 0);
      })
      .catch(() => setResults([]))
      .finally(() => setLoading(false));
  }, [debouncedQuery]);

  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setIsOpen(false);
      }
    }
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  function handleSelect(part: PartInfo) {
    suppressSearch.current = true;
    setQuery(part.itemKey);
    setIsOpen(false);
    onSelect(part);
  }

  return (
    <div ref={containerRef} className="relative">
      <input
        type="text"
        value={query}
        onChange={e => setQuery(e.target.value.toUpperCase())}
        placeholder="Enter item key..."
        className="w-full border border-gray-300 rounded-lg px-3 py-2.5 text-sm focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
      />
      {loading && (
        <div className="absolute right-3 top-3 text-xs text-gray-400">Searching...</div>
      )}
      {isOpen && results.length > 0 && (
        <ul className="absolute z-50 w-full mt-1 bg-white border border-gray-200 rounded-lg shadow-lg max-h-60 overflow-y-auto">
          {results.map(part => (
            <li
              key={part.itemKey}
              onClick={() => handleSelect(part)}
              className="px-3 py-2 hover:bg-blue-50 cursor-pointer border-b border-gray-100 last:border-0"
            >
              <span className="font-semibold text-sm">{part.itemKey}</span>
              <span className="text-gray-500 text-xs ml-2">
                {part.hebrewDescription || part.partName}
              </span>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
