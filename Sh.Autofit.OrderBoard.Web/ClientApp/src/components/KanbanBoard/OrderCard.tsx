import { useDraggable } from '@dnd-kit/core';
import { CSS } from '@dnd-kit/utilities';
import type { OrderCard as OrderCardType } from '../../types';
import { pinOrder, unpinOrder, hideOrder, moveOrder } from '../../api/client';

type SlaStatus = 'none' | 'green' | 'yellow' | 'red';

function getSlaStatus(stage: string, stageUpdatedAt: string): SlaStatus {
  if (stage !== 'ORDER_PRINTED' && stage !== 'PACKING' && stage !== 'PACKED') return 'none';
  const mins = (Date.now() - new Date(stageUpdatedAt).getTime()) / 60000;
  if (mins < 30) return 'green';
  if (mins < 60) return 'yellow';
  return 'red';
}

const SLA_BORDER: Record<SlaStatus, string> = {
  none: '',
  green: 'border-r-4 border-r-green-500',
  yellow: 'border-r-4 border-r-yellow-500',
  red: 'border-r-4 border-r-red-500',
};

interface Props {
  order: OrderCardType;
  onAction: () => void;
  onClick?: () => void;
  showPackedButton?: boolean;
}

export default function OrderCard({ order, onAction, onClick, showPackedButton }: Props) {
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

  const handlePacked = async (e: React.MouseEvent) => {
    e.stopPropagation();
    try {
      await moveOrder(order.appOrderId, 'PACKED');
      onAction();
    } catch (err) {
      console.error('Pack error:', err);
    }
  };

  const handleClick = () => {
    if (onClick) onClick();
  };

  const createdAtStr = order.createdAt
    ? new Date(order.createdAt).toLocaleTimeString('he-IL', { hour: '2-digit', minute: '2-digit' })
    : '';
  const stageTimeStr = order.stageUpdatedAt
    ? new Date(order.stageUpdatedAt).toLocaleTimeString('he-IL', { hour: '2-digit', minute: '2-digit' })
    : '';

  const slaStatus = getSlaStatus(order.currentStage, order.stageUpdatedAt);
  const slaBorder = SLA_BORDER[slaStatus];

  return (
    <div
      ref={setNodeRef}
      style={style}
      onClick={handleClick}
      className={`relative p-1.5 border rounded-lg shadow-sm
        hover:shadow-md transition-shadow select-none
        ${slaBorder}
        ${order.hasPackedSibling ? 'bg-yellow-300' : order.isManual ? 'bg-yellow-50' : 'bg-white'}
        ${order.pinned ? 'border-amber-400 border-2' : 'border-gray-200'}
        ${order.hidden ? 'opacity-60' : ''}
        ${order.needsResolve ? 'ring-2 ring-red-400' : ''}`}
    >
      {/* Stack badge */}
      {order.stackCount > 1 && (
        <span className="absolute -top-2 -left-2 bg-red-500 text-white text-[10px] font-bold rounded-full w-5 h-5 flex items-center justify-center">
          {order.stackCount}
        </span>
      )}

      {/* Row 1: Drag handle + AccountKey + actions */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-1">
          <div
            {...listeners}
            {...attributes}
            className="flex items-center justify-center w-5 h-6 cursor-grab active:cursor-grabbing text-gray-300 hover:text-gray-500 touch-none shrink-0"
            title="גרור"
          >
            <svg width="10" height="16" viewBox="0 0 10 16" fill="currentColor">
              <circle cx="2" cy="2" r="1.5"/>
              <circle cx="8" cy="2" r="1.5"/>
              <circle cx="2" cy="8" r="1.5"/>
              <circle cx="8" cy="8" r="1.5"/>
              <circle cx="2" cy="14" r="1.5"/>
              <circle cx="8" cy="14" r="1.5"/>
            </svg>
          </div>
          <span className="font-black text-lg text-gray-900 leading-tight">{order.accountKey}</span>
        </div>
        <div className="flex gap-0.5">
          <button
            onClick={handlePin}
            onPointerDown={(e) => e.stopPropagation()}
            className={`text-xs px-0.5 ${order.pinned ? 'text-amber-500' : 'text-gray-300 hover:text-amber-500'}`}
            title={order.pinned ? 'בטל נעיצה' : 'נעץ'}
          >
            📌
          </button>
          <button
            onClick={handleHide}
            onPointerDown={(e) => e.stopPropagation()}
            className="text-xs px-0.5 text-gray-300 hover:text-red-500"
            title="הסתר"
          >
            ✕
          </button>
        </div>
      </div>

      {/* Row 2: AccountName */}
      <div className="text-lg font-semibold text-gray-700 truncate leading-tight">{order.accountName}</div>

      {/* Row 3: City/Address */}
      {(order.city || order.address) && (
        <div className="text-[10px] text-gray-400 truncate">
          {[order.city, order.address].filter(Boolean).join(' · ')}
        </div>
      )}

      {/* Row 4: Times + Badges */}
      <div className="flex items-center justify-between mt-0.5">
        <div className="flex items-center gap-1.5 text-[10px] text-gray-400">
          {createdAtStr && <span title="זמן קליטה">{createdAtStr}</span>}
          {stageTimeStr && createdAtStr !== stageTimeStr && (
            <span title="כניסה לשלב" className="text-gray-500 font-medium">{stageTimeStr}</span>
          )}
        </div>
        <div className="flex gap-0.5 flex-wrap justify-end">
          {order.isManual && (
            <span className="text-[10px] bg-yellow-200 text-yellow-800 px-1 py-0.5 rounded">ידני</span>
          )}
          {order.deliveryMethodName && (
            <span className="text-[10px] bg-blue-100 text-blue-700 px-1 py-0.5 rounded truncate max-w-[70px]">
              {order.deliveryMethodName}
            </span>
          )}
          {order.closestRuleName && !order.deliveryMethodName && (
            <span
              className="text-[10px] bg-emerald-100 text-emerald-700 px-1 py-0.5 rounded truncate max-w-[90px]"
              title={order.closestRuleWindow ?? ''}
            >
              {order.closestRuleName}
            </span>
          )}
        </div>
      </div>

      {/* Manual note */}
      {order.manualNote && (
        <div className="text-[10px] text-orange-600 truncate" title={order.manualNote}>
          📝 {order.manualNote}
        </div>
      )}

      {/* Packed button — shown in PACKING stage */}
      {showPackedButton && (
        <button
          onClick={handlePacked}
          onPointerDown={(e) => e.stopPropagation()}
          className="mt-1 w-full text-xs font-bold py-1 rounded bg-teal-500 text-white hover:bg-teal-600 transition-colors"
        >
          נארז ✓
        </button>
      )}
    </div>
  );
}
