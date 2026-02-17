import { useDroppable } from '@dnd-kit/core';
import type { DeliveryGroup } from '../../types';

interface Props {
  groups: DeliveryGroup[];
  stageId: string;
}

function QuickAssignPill({ group, stageId }: { group: DeliveryGroup; stageId: string }) {
  const droppableId = group.deliveryRunId
    ? `delivery-run-${group.deliveryRunId}`
    : group.deliveryMethodId
      ? `delivery-method-${group.deliveryMethodId}`
      : `${stageId}-unassigned`;

  const { setNodeRef, isOver } = useDroppable({
    id: `quick-${droppableId}`,
    data: {
      type: 'delivery-group',
      deliveryMethodId: group.deliveryMethodId,
      deliveryRunId: group.deliveryRunId,
      stage: stageId,
    },
  });

  const isUnassigned = !group.deliveryMethodId && !group.deliveryRunId;

  return (
    <div
      ref={setNodeRef}
      className={`flex-shrink-0 px-3 py-1.5 rounded-full text-xs font-medium cursor-default transition-all
        ${isOver
          ? 'bg-green-500 text-white scale-110 shadow-lg'
          : isUnassigned
            ? 'bg-gray-200 text-gray-600'
            : 'bg-blue-100 text-blue-700'
        }`}
    >
      {group.title} ({group.count})
    </div>
  );
}

export default function QuickAssignBar({ groups, stageId }: Props) {
  return (
    <div className="flex gap-2 px-3 py-2 bg-green-50 border-b border-green-200 overflow-x-auto">
      {groups.map((group, i) => (
        <QuickAssignPill
          key={`${group.deliveryMethodId ?? 'u'}-${group.deliveryRunId ?? 'u'}-${i}`}
          group={group}
          stageId={stageId}
        />
      ))}
    </div>
  );
}
