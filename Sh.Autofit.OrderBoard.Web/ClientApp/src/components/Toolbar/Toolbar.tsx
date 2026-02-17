import { useState } from 'react';
import type { DeliveryMethodDto } from '../../types';
import CreateManualOrderModal from '../Modals/CreateManualOrderModal';
import CreateDeliveryMethodModal from '../Modals/CreateDeliveryMethodModal';

interface Props {
  searchQuery: string;
  onSearchChange: (q: string) => void;
  includeArchived: boolean;
  onToggleArchived: () => void;
  onRefresh: () => void;
  deliveryMethods: DeliveryMethodDto[];
}

export default function Toolbar({
  searchQuery,
  onSearchChange,
  includeArchived,
  onToggleArchived,
  onRefresh,
  deliveryMethods: _deliveryMethods,
}: Props) {
  const [showManualModal, setShowManualModal] = useState(false);
  const [showDeliveryModal, setShowDeliveryModal] = useState(false);

  return (
    <>
      <div className="bg-white shadow-sm border-b px-4 py-3 flex flex-wrap items-center gap-3">
        {/* Search */}
        <div className="relative flex-1 min-w-[200px] max-w-[400px]">
          <input
            type="text"
            value={searchQuery}
            onChange={(e) => onSearchChange(e.target.value)}
            placeholder="חיפוש לפי לקוח, עיר, כתובת..."
            className="w-full px-3 py-2 pr-8 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
          {searchQuery && (
            <button
              onClick={() => onSearchChange('')}
              className="absolute left-2 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
            >
              ✕
            </button>
          )}
        </div>

        {/* Actions */}
        <button
          onClick={() => setShowManualModal(true)}
          className="px-3 py-2 bg-blue-600 text-white text-sm rounded-lg hover:bg-blue-700 transition-colors"
        >
          + הזמנה ידנית
        </button>

        <button
          onClick={() => setShowDeliveryModal(true)}
          className="px-3 py-2 bg-purple-600 text-white text-sm rounded-lg hover:bg-purple-700 transition-colors"
        >
          + שיטת משלוח
        </button>

        <button
          onClick={onToggleArchived}
          className={`px-3 py-2 text-sm rounded-lg transition-colors border ${
            includeArchived
              ? 'bg-gray-200 text-gray-700 border-gray-300'
              : 'bg-white text-gray-500 border-gray-300 hover:bg-gray-50'
          }`}
        >
          {includeArchived ? 'הסתר ארכיון' : 'הצג ארכיון'}
        </button>

        <button
          onClick={onRefresh}
          className="px-3 py-2 text-sm text-gray-500 hover:text-gray-700 border border-gray-300 rounded-lg hover:bg-gray-50"
          title="רענן"
        >
          ↻
        </button>
      </div>

      {showManualModal && (
        <CreateManualOrderModal
          onClose={() => setShowManualModal(false)}
          onCreated={onRefresh}
        />
      )}

      {showDeliveryModal && (
        <CreateDeliveryMethodModal
          onClose={() => setShowDeliveryModal(false)}
          onCreated={onRefresh}
        />
      )}
    </>
  );
}
