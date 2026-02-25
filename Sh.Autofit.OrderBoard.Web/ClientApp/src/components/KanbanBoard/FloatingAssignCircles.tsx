import { useDroppable } from '@dnd-kit/core';
import type { DeliveryGroup } from '../../types';

interface Props {
  groups: DeliveryGroup[];
  stageId: string;
}

function FloatingCircle({ group, stageId }: { group: DeliveryGroup; stageId: string }) {
  const droppableId = group.deliveryRunId
    ? `delivery-run-${group.deliveryRunId}`
    : group.deliveryMethodId
      ? `delivery-method-${group.deliveryMethodId}`
      : `${stageId}-unassigned`;

  const { setNodeRef, isOver } = useDroppable({
    id: `float-${droppableId}`,
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
      className={`w-16 h-16 rounded-full flex items-center justify-center text-center
        text-[10px] font-bold shadow-lg transition-all cursor-default leading-tight p-1
        ${isOver
          ? 'bg-green-500 text-white scale-125 ring-4 ring-green-300'
          : 'bg-white text-gray-700 border-2 border-blue-300 hover:border-blue-500'}`}
    >
      {group.title}
    </div>
  );
}

export default function FloatingAssignCircles({ groups, stageId }: Props) {
  const deliveryGroups = groups.filter((g) => g.deliveryMethodId !== null);
  if (deliveryGroups.length === 0) return null;

  return (
    <div className="fixed left-4 top-1/2 -translate-y-1/2 z-40 flex flex-col gap-3">
      {deliveryGroups.map((group, i) => (
        <FloatingCircle
          key={`${group.deliveryMethodId}-${group.deliveryRunId}-${i}`}
          group={group}
          stageId={stageId}
        />
      ))}
    </div>
  );
}
