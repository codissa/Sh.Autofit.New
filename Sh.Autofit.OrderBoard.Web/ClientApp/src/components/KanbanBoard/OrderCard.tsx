import { useDraggable } from '@dnd-kit/core';
import { CSS } from '@dnd-kit/utilities';
import type { OrderCard as OrderCardType } from '../../types';
import { pinOrder, unpinOrder, hideOrder } from '../../api/client';

interface Props {
  order: OrderCardType;
  onAction: () => void;
}

export default function OrderCard({ order, onAction }: Props) {
  const { attributes, listeners, setNodeRef, transform, isDragging } = useDraggable({
    id: `card-${order.appOrderId}`,
    data: { type: 'card', order },
  });

  const style = {
    transform: CSS.Translate.toString(transform),
    opacity: isDragging ? 0.4 : 1,
  };

  const handlePin = async (e: React.MouseEvent) => {
    e.stopPropagation();
    try {
      if (order.pinned) {
        await unpinOrder(order.appOrderId);
      } else {
        await pinOrder(order.appOrderId);
      }
      onAction();
    } catch (err) {
      console.error('Pin error:', err);
    }
  };

  const handleHide = async (e: React.MouseEvent) => {
    e.stopPropagation();
    try {
      await hideOrder(order.appOrderId);
      onAction();
    } catch (err) {
      console.error('Hide error:', err);
    }
  };

  const timeStr = order.displayTime
    ? new Date(order.displayTime).toLocaleTimeString('he-IL', {
        hour: '2-digit',
        minute: '2-digit',
      })
    : '';

  return (
    <div
      ref={setNodeRef}
      style={style}
      {...listeners}
      {...attributes}
      className={`relative p-2 bg-white border rounded-lg shadow-sm cursor-grab active:cursor-grabbing
        hover:shadow-md transition-shadow select-none
        ${order.pinned ? 'border-amber-400 border-2' : 'border-gray-200'}
        ${order.isManual ? 'bg-yellow-50' : ''}
        ${order.hidden ? 'opacity-60' : ''}
        ${order.needsResolve ? 'ring-2 ring-red-400' : ''}`}
    >
      {/* Stack badge */}
      {order.stackCount > 1 && (
        <span className="absolute -top-2 -left-2 bg-red-500 text-white text-xs font-bold rounded-full w-6 h-6 flex items-center justify-center">
          {order.stackCount}
        </span>
      )}

      {/* Header row */}
      <div className="flex items-center justify-between mb-0.5">
        <span className="font-bold text-base text-gray-900">{order.accountKey}</span>
        <div className="flex gap-1">
          <button
            onClick={handlePin}
            onPointerDown={(e) => e.stopPropagation()}
            className={`text-xs px-1 rounded ${order.pinned ? 'text-amber-500' : 'text-gray-400 hover:text-amber-500'}`}
            title={order.pinned ? 'בטל נעיצה' : 'נעץ'}
          >
            📌
          </button>
          <button
            onClick={handleHide}
            onPointerDown={(e) => e.stopPropagation()}
            className="text-xs px-1 text-gray-400 hover:text-red-500 rounded"
            title="הסתר"
          >
            ✕
          </button>
        </div>
      </div>

      {/* Name */}
      <div className="text-base text-gray-700 truncate">{order.accountName}</div>

      {/* City + Address */}
      {(order.city || order.address) && (
        <div className="text-xs text-gray-500 mt-1 truncate">
          {[order.city, order.address].filter(Boolean).join(', ')}
        </div>
      )}

      {/* Bottom row: time + badges */}
      <div className="flex items-center justify-between mt-1">
        <span className="text-xs text-gray-400">{timeStr}</span>
        <div className="flex gap-1">
          {order.isManual && (
            <span className="text-xs bg-yellow-200 text-yellow-800 px-1.5 py-0.5 rounded">ידני</span>
          )}
          {order.deliveryMethodName && (
            <span className="text-xs bg-blue-100 text-blue-700 px-1.5 py-0.5 rounded truncate max-w-[80px]">
              {order.deliveryMethodName}
            </span>
          )}
        </div>
      </div>

      {/* Manual note */}
      {order.manualNote && (
        <div className="text-xs text-orange-600 mt-1 truncate" title={order.manualNote}>
          📝 {order.manualNote}
        </div>
      )}
    </div>
  );
}
