import { useCallback, useState } from 'react';
import { useDroppable } from '@dnd-kit/core';
import type { BoardColumn } from '../../types';
import { STAGE_COLORS, type OrderStage } from '../../types';
import { useAutoScroll } from '../../hooks/useAutoScroll';
import { bulkHideByStage } from '../../api/client';
import DeliveryGroup from './DeliveryGroup';
import QuickAssignBar from './QuickAssignBar';
import OrderCard from './OrderCard';

interface Props {
  column: BoardColumn;
  onAction: () => void;
  isDragging?: boolean;
}

export default function KanbanColumn({ column, onAction, isDragging }: Props) {
  const [confirming, setConfirming] = useState(false);
  const { setNodeRef: setDropRef, isOver } = useDroppable({
    id: `stage-${column.stage}`,
    data: { type: 'stage', stage: column.stage },
  });

  const autoScrollRef = useAutoScroll();

  const combinedRef = useCallback(
    (node: HTMLDivElement | null) => {
      setDropRef(node);
      autoScrollRef(node);
    },
    [setDropRef, autoScrollRef]
  );

  const handleClean = async () => {
    if (!confirming) {
      setConfirming(true);
      setTimeout(() => setConfirming(false), 3000);
      return;
    }
    try {
      await bulkHideByStage(column.stage);
      onAction();
    } catch (err) {
      console.error('Bulk hide failed:', err);
    }
    setConfirming(false);
  };

  const colorClass = STAGE_COLORS[column.stage as OrderStage] || 'bg-gray-600';
  const isFlat = column.groups.length === 1 && column.groups[0].title === '';
  const isPacking = column.stage === 'PACKING';

  return (
    <div
      className={`flex flex-col flex-1 min-w-[280px] rounded-xl shadow-lg bg-white overflow-hidden
        ${isOver ? 'ring-2 ring-blue-400' : ''}`}
    >
      {/* Column header */}
      <div className={`${colorClass} text-white px-4 py-3 flex items-center justify-between`}>
        <div className="flex items-center gap-2">
          <h2 className="font-bold text-sm">{column.label}</h2>
          {column.count > 0 && (
            <button
              onClick={handleClean}
              title={confirming ? 'לחץ שוב לאישור' : 'נקה עמודה'}
              className={`text-white/70 hover:text-white transition-colors text-xs px-1.5 py-0.5 rounded
                ${confirming ? 'bg-red-500/60 text-white animate-pulse' : 'hover:bg-white/20'}`}
            >
              {confirming ? 'אישור?' : '🧹'}
            </button>
          )}
        </div>
        <span className="bg-white/20 text-white text-xs font-bold rounded-full px-2.5 py-0.5">
          {column.count}
        </span>
      </div>

      {/* Quick-assign bar for PACKING during drag */}
      {isPacking && isDragging && (
        <QuickAssignBar groups={column.groups} stageId={column.stage} />
      )}

      {/* Column body */}
      <div ref={combinedRef} className="flex-1 p-3 overflow-y-auto min-h-[200px] max-h-[calc(100vh-160px)]">
        {isFlat ? (
          /* Flat rendering: cards directly, no group wrapper */
          <div className="space-y-2">
            {column.groups[0].orders.map((order) => (
              <OrderCard key={order.appOrderId} order={order} onAction={onAction} />
            ))}
            {column.groups[0].orders.length === 0 && (
              <div className="text-sm text-gray-400 text-center py-8">
                אין הזמנות
              </div>
            )}
          </div>
        ) : (
          /* Grouped rendering: delivery groups */
          <>
            {column.groups.map((group, i) => (
              <DeliveryGroup
                key={`${group.deliveryMethodId ?? 'u'}-${group.deliveryRunId ?? 'u'}-${i}`}
                group={group}
                stageId={column.stage}
                onAction={onAction}
              />
            ))}
            {column.groups.length === 0 && (
              <div className="text-sm text-gray-400 text-center py-8">
                אין הזמנות
              </div>
            )}
          </>
        )}
      </div>
    </div>
  );
}
