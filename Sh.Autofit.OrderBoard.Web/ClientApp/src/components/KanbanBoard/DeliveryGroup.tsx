import { useDroppable } from '@dnd-kit/core';
import type { DeliveryGroup as DeliveryGroupType, OrderCard as OrderCardType } from '../../types';
import OrderCard from './OrderCard';

interface Props {
  group: DeliveryGroupType;
  stageId: string;
  onAction: () => void;
  onCardClick?: (order: OrderCardType) => void;
  showPackedButton?: boolean;
}

export default function DeliveryGroup({ group, stageId, onAction, onCardClick, showPackedButton }: Props) {
  const droppableId = group.deliveryRunId
    ? `delivery-run-${group.deliveryRunId}`
    : group.deliveryMethodId
      ? `delivery-method-${group.deliveryMethodId}`
      : `${stageId}-unassigned`;

  const { setNodeRef, isOver } = useDroppable({
    id: droppableId,
    data: {
      type: 'delivery-group',
      deliveryMethodId: group.deliveryMethodId,
      deliveryRunId: group.deliveryRunId,
      stage: stageId,
    },
  });

  return (
    <div
      ref={setNodeRef}
      className={`rounded-lg border-2 border-dashed p-2 mb-3 transition-colors
        ${isOver ? 'border-blue-400 bg-blue-50' : 'border-gray-200 bg-gray-50'}`}
    >
      {/* Group header */}
      <div className="flex items-center justify-between mb-2 px-1">
        <div className="flex items-center gap-1.5">
          <span className="text-xs font-semibold text-gray-600">{group.title}</span>
          {group.timeWindow && (
            <span className="text-xs text-gray-400">({group.timeWindow})</span>
          )}
        </div>
        <span className="text-xs text-gray-400 bg-gray-200 rounded-full px-2 py-0.5">
          {group.count}
        </span>
      </div>

      {/* Cards */}
      <div className="space-y-1.5">
        {group.orders.map((order) => (
          <OrderCard
            key={order.appOrderId}
            order={order}
            onAction={onAction}
            onClick={onCardClick ? () => onCardClick(order) : undefined}
            showPackedButton={showPackedButton}
          />
        ))}
        {group.orders.length === 0 && (
          <div className="text-xs text-gray-400 text-center py-3">
            גרור הזמנות לכאן
          </div>
        )}
      </div>
    </div>
  );
}
