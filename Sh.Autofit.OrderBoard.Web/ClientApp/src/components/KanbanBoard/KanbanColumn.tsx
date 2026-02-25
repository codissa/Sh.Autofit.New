import { useCallback, useRef, useState } from 'react';
import { useDroppable } from '@dnd-kit/core';
import type { BoardColumn, DeliveryMethodDto, OrderCard as OrderCardType } from '../../types';
import { STAGE_COLORS, type OrderStage } from '../../types';
import { useAutoScroll } from '../../hooks/useAutoScroll';
import { useColumnSortFilter } from '../../hooks/useColumnSortFilter';
import { bulkHideByStage } from '../../api/client';
import DeliveryGroup from './DeliveryGroup';
import QuickAssignBar from './QuickAssignBar';
import OrderCard from './OrderCard';
import ScrollButtons from './ScrollButtons';
import ColumnSortFilter from './ColumnSortFilter';

interface Props {
  column: BoardColumn;
  onAction: () => void;
  isDragging?: boolean;
  deliveryMethods: DeliveryMethodDto[];
  onCardClick?: (order: OrderCardType) => void;
}

export default function KanbanColumn({ column, onAction, isDragging, deliveryMethods, onCardClick }: Props) {
  const [confirming, setConfirming] = useState(false);
  const { setNodeRef: setDropRef, isOver } = useDroppable({
    id: `stage-${column.stage}`,
    data: { type: 'stage', stage: column.stage },
  });

  const { setRef: autoScrollRef, resetIdle } = useAutoScroll();
  const scrollContainerRef = useRef<HTMLDivElement | null>(null);

  const combinedRef = useCallback(
    (node: HTMLDivElement | null) => {
      setDropRef(node);
      autoScrollRef(node);
      scrollContainerRef.current = node;
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

  // Flatten all orders across groups for sort/filter
  const allOrders = column.groups.flatMap((g) => g.orders);
  const { processedOrders, sortOption, setSortOption, filterMethodId, setFilterMethodId } =
    useColumnSortFilter(allOrders);

  const colorClass = STAGE_COLORS[column.stage as OrderStage] || 'bg-gray-600';
  const isFlat = column.groups.length === 1 && column.groups[0].title === '';
  const isPacking = column.stage === 'PACKING';
  const isPacked = column.stage === 'PACKED';
  const hasDeliveryGroups = isPacking || isPacked;
  const isCustomSort = sortOption !== 'default' || filterMethodId !== null;

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

      {/* Sort/Filter bar */}
      <ColumnSortFilter
        currentSort={sortOption}
        onSortChange={setSortOption}
        currentFilter={filterMethodId}
        onFilterChange={setFilterMethodId}
        deliveryMethods={deliveryMethods}
      />

      {/* Quick-assign bar for PACKING/PACKED during drag */}
      {hasDeliveryGroups && isDragging && (
        <QuickAssignBar groups={column.groups} stageId={column.stage} />
      )}

      {/* Column body with scroll buttons */}
      <div className="relative flex-1">
        <ScrollButtons containerRef={scrollContainerRef} onInteraction={resetIdle} />
        <div ref={combinedRef} className="flex-1 p-2 overflow-y-auto min-h-[200px] max-h-[calc(100vh-210px)]">
          {isCustomSort || isFlat ? (
            /* Flat/sorted rendering: cards directly */
            <div className="space-y-1.5">
              {(isCustomSort ? processedOrders : column.groups[0].orders).map((order) => (
                <OrderCard
                  key={order.appOrderId}
                  order={order}
                  onAction={onAction}
                  onClick={hasDeliveryGroups && onCardClick ? () => onCardClick(order) : undefined}
                  showPackedButton={isPacking}
                />
              ))}
              {(isCustomSort ? processedOrders : column.groups[0].orders).length === 0 && (
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
                  onCardClick={hasDeliveryGroups && onCardClick ? onCardClick : undefined}
                  showPackedButton={isPacking}
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
    </div>
  );
}
