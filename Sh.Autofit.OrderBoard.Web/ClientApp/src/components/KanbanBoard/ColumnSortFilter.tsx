export type SortOption = 'default' | 'newest' | 'oldest' | 'accountName' | 'city';

interface Props {
  currentSort: SortOption;
  onSortChange: (sort: SortOption) => void;
  currentFilter: number | null;
  onFilterChange: (methodId: number | null) => void;
  deliveryMethods: Array<{ deliveryMethodId: number; name: string }>;
}

const SORT_BUTTONS: { key: SortOption; label: string }[] = [
  { key: 'default', label: 'ברירת מחדל' },
  { key: 'newest', label: 'חדש' },
  { key: 'oldest', label: 'ישן' },
  { key: 'accountName', label: 'שם' },
  { key: 'city', label: 'עיר' },
];

export default function ColumnSortFilter({
  currentSort,
  onSortChange,
  currentFilter,
  onFilterChange,
  deliveryMethods,
}: Props) {
  return (
    <div className="flex items-center gap-1 px-2 py-1.5 bg-gray-50 border-b border-gray-200 overflow-x-auto">
      <div className="flex gap-0.5">
        {SORT_BUTTONS.map(({ key, label }) => (
          <button
            key={key}
            onClick={() => onSortChange(key)}
            className={`text-[10px] px-1.5 py-1 rounded transition-colors whitespace-nowrap
              ${currentSort === key
                ? 'bg-blue-600 text-white'
                : 'bg-white text-gray-500 hover:bg-gray-200 border border-gray-200'}`}
          >
            {label}
          </button>
        ))}
      </div>

      {deliveryMethods.length > 0 && (
        <select
          value={currentFilter ?? ''}
          onChange={(e) => onFilterChange(e.target.value ? parseInt(e.target.value) : null)}
          className="text-[10px] px-1 py-1 border border-gray-200 rounded bg-white text-gray-600 min-w-0"
        >
          <option value="">הכל</option>
          {deliveryMethods.map((m) => (
            <option key={m.deliveryMethodId} value={m.deliveryMethodId}>
              {m.name}
            </option>
          ))}
        </select>
      )}
    </div>
  );
}
