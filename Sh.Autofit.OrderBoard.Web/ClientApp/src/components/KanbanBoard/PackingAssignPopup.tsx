import { useRef } from 'react';
import type { OrderCard as OrderCardType, DeliveryGroup } from '../../types';

interface Props {
  order: OrderCardType;
  groups: DeliveryGroup[];
  onAssign: (deliveryMethodId: number | null, deliveryRunId: number | null) => void;
  onClose: () => void;
}

export default function PackingAssignPopup({ order, groups, onAssign, onClose }: Props) {
  const openedAt = useRef(Date.now());

  const handleOverlayClick = () => {
    if (Date.now() - openedAt.current > 300) onClose();
  };

  return (
    <div className="fixed inset-0 bg-black/40 z-50 flex items-center justify-center" onClick={handleOverlayClick}>
      <div
        className="bg-white rounded-2xl shadow-2xl p-6 min-w-[280px] max-w-[90vw] max-h-[80vh] overflow-y-auto"
        onClick={(e) => e.stopPropagation()}
      >
        <h3 className="text-lg font-bold text-gray-800 mb-4 text-center">
          שייך: {order.accountName ?? order.accountKey}
        </h3>
        <div className="flex flex-col gap-3">
          {groups
            .filter((g) => g.deliveryMethodId !== null)
            .map((group, i) => (
              <button
                key={`${group.deliveryMethodId}-${group.deliveryRunId}-${i}`}
                onClick={() => onAssign(group.deliveryMethodId, group.deliveryRunId)}
                className="px-6 py-4 text-lg font-semibold rounded-xl border-2 transition-colors
                  bg-blue-50 border-blue-200 text-blue-800 hover:bg-blue-100 active:bg-blue-200"
              >
                {group.title}
                {group.timeWindow && (
                  <span className="text-sm text-blue-500 mr-2"> ({group.timeWindow})</span>
                )}
                <span className="text-sm text-gray-400 mr-2"> ({group.count})</span>
              </button>
            ))}
          <button
            onClick={() => onAssign(null, null)}
            className="px-6 py-4 text-lg font-semibold rounded-xl border-2 transition-colors
              bg-gray-50 border-gray-200 text-gray-600 hover:bg-gray-100 active:bg-gray-200"
          >
            לא משויך
          </button>
        </div>
        <button
          onClick={onClose}
          className="mt-4 w-full py-3 text-base text-gray-500 hover:text-gray-700 border border-gray-200 rounded-xl"
        >
          ביטול
        </button>
      </div>
    </div>
  );
}
