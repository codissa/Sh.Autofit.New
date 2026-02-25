import { useEffect, useCallback, useState } from 'react';
import {
  DndContext,
  DragOverlay,
  PointerSensor,
  TouchSensor,
  useSensor,
  useSensors,
  type DragEndEvent,
  type DragStartEvent,
} from '@dnd-kit/core';
import { useBoardState } from '../../hooks/useBoardState';
import { useSignalR } from '../../hooks/useSignalR';
import { moveOrder, assignDelivery } from '../../api/client';
import type { OrderCard as OrderCardType } from '../../types';
import KanbanColumn from './KanbanColumn';
import OrderCard from './OrderCard';
import Toolbar from '../Toolbar/Toolbar';
import FloatingAssignCircles from './FloatingAssignCircles';
import PackingAssignPopup from './PackingAssignPopup';

export default function KanbanBoard() {
  const { board, loading, error, includeArchived, refresh, toggleArchived } = useBoardState();
  const [activeCard, setActiveCard] = useState<OrderCardType | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [assignTarget, setAssignTarget] = useState<OrderCardType | null>(null);

  // Initial load
  useEffect(() => {
    refresh();
  }, [refresh]);

  // SignalR: refresh board on any server event
  const handleBoardChanged = useCallback(() => {
    refresh();
  }, [refresh]);

  useSignalR(handleBoardChanged);

  // Periodic refresh for SLA color accuracy (every 60 seconds)
  useEffect(() => {
    const interval = setInterval(() => {
      refresh();
    }, 60000);
    return () => clearInterval(interval);
  }, [refresh]);

  // Drag sensors
  const pointerSensor = useSensor(PointerSensor, {
    activationConstraint: { distance: 10 },
  });
  const touchSensor = useSensor(TouchSensor, {
    activationConstraint: { delay: 300, tolerance: 5 },
  });
  const sensors = useSensors(pointerSensor, touchSensor);

  const handleDragStart = (event: DragStartEvent) => {
    const card = event.active.data.current?.order as OrderCardType | undefined;
    if (card) setActiveCard(card);
  };

  const handleDragEnd = async (event: DragEndEvent) => {
    setActiveCard(null);
    const { active, over } = event;
    if (!over) return;

    const card = active.data.current?.order as OrderCardType | undefined;
    if (!card) return;

    const overData = over.data.current;
    // Strip 'quick-' or 'float-' prefix from assign bar/circle pills
    const rawOverId = over.id as string;
    const overId = rawOverId.startsWith('quick-')
      ? rawOverId.slice(6)
      : rawOverId.startsWith('float-')
        ? rawOverId.slice(6)
        : rawOverId;

    try {
      if (overId.startsWith('stage-')) {
        // Dropped on a stage column -> move stage
        const toStage = overId.replace('stage-', '');
        if (toStage !== card.currentStage) {
          await moveOrder(card.appOrderId, toStage);
        }
      } else if (overId.startsWith('delivery-run-') || overId.startsWith('delivery-method-')) {
        // Dropped on a delivery group -> assign delivery
        const methodId = overData?.deliveryMethodId as number | null;
        const runId = overData?.deliveryRunId as number | null;
        const targetStage = overData?.stage as string | undefined;

        // Also move stage if target group is in a different column
        if (targetStage && targetStage !== card.currentStage) {
          await moveOrder(card.appOrderId, targetStage);
        }
        await assignDelivery(card.appOrderId, methodId, runId);
      } else if (overId.endsWith('-unassigned')) {
        // Dropped on unassigned group -> unassign delivery
        const targetStage = overData?.stage as string | undefined;
        if (targetStage && targetStage !== card.currentStage) {
          await moveOrder(card.appOrderId, targetStage);
        }
        await assignDelivery(card.appOrderId, null, null);
      }
    } catch (err) {
      console.error('Drag action failed:', err);
    }

    // Refresh will happen via SignalR broadcast
  };

  // Packing assignment via popup
  const handleAssignFromPopup = async (
    deliveryMethodId: number | null,
    deliveryRunId: number | null
  ) => {
    if (!assignTarget) return;
    try {
      await assignDelivery(assignTarget.appOrderId, deliveryMethodId, deliveryRunId);
    } catch (err) {
      console.error('Assign failed:', err);
    }
    setAssignTarget(null);
  };

  // Get groups for popup and floating circles (PACKING + PACKED)
  const packingColumn = board?.columns.find((c) => c.stage === 'PACKING');
  const packedColumn = board?.columns.find((c) => c.stage === 'PACKED');
  const packingGroups = packingColumn?.groups ?? [];
  const packedGroups = packedColumn?.groups ?? [];
  const allDeliveryGroups = [...packingGroups, ...packedGroups];

  // Filter columns by search
  const filteredColumns = board?.columns.map((col) => {
    if (!searchQuery.trim()) return col;

    const q = searchQuery.trim().toLowerCase();
    return {
      ...col,
      groups: col.groups.map((g) => ({
        ...g,
        orders: g.orders.filter(
          (o) =>
            o.accountKey.toLowerCase().includes(q) ||
            (o.accountName?.toLowerCase().includes(q) ?? false) ||
            (o.city?.toLowerCase().includes(q) ?? false) ||
            (o.address?.toLowerCase().includes(q) ?? false)
        ),
        count: g.orders.filter(
          (o) =>
            o.accountKey.toLowerCase().includes(q) ||
            (o.accountName?.toLowerCase().includes(q) ?? false) ||
            (o.city?.toLowerCase().includes(q) ?? false) ||
            (o.address?.toLowerCase().includes(q) ?? false)
        ).length,
      })),
      count: col.groups.reduce(
        (sum, g) =>
          sum +
          g.orders.filter(
            (o) =>
              o.accountKey.toLowerCase().includes(q) ||
              (o.accountName?.toLowerCase().includes(q) ?? false) ||
              (o.city?.toLowerCase().includes(q) ?? false) ||
              (o.address?.toLowerCase().includes(q) ?? false)
          ).length,
        0
      ),
    };
  });

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-screen bg-gray-100">
        <div className="text-xl text-gray-500">טוען לוח הזמנות...</div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="flex items-center justify-center min-h-screen bg-gray-100">
        <div className="text-center">
          <div className="text-xl text-red-500 mb-2">שגיאה בטעינת הלוח</div>
          <div className="text-sm text-gray-500">{error}</div>
          <button
            onClick={refresh}
            className="mt-4 px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700"
          >
            נסה שוב
          </button>
        </div>
      </div>
    );
  }

  return (
    <div>
      <Toolbar
        searchQuery={searchQuery}
        onSearchChange={setSearchQuery}
        includeArchived={includeArchived}
        onToggleArchived={toggleArchived}
        onRefresh={refresh}
        deliveryMethods={board?.deliveryMethods ?? []}
      />

      <DndContext
        sensors={sensors}
        onDragStart={handleDragStart}
        onDragEnd={handleDragEnd}
      >
        <div className="flex gap-4 p-4 pb-8">
          {filteredColumns?.map((col) => (
            <KanbanColumn
              key={col.stage}
              column={col}
              onAction={refresh}
              isDragging={!!activeCard}
              deliveryMethods={board?.deliveryMethods ?? []}
              onCardClick={col.stage === 'PACKING' || col.stage === 'PACKED' ? setAssignTarget : undefined}
            />
          ))}
        </div>

        {/* Floating circles during drag for delivery assignment */}
        {activeCard && allDeliveryGroups.length > 0 && (
          <FloatingAssignCircles groups={allDeliveryGroups} stageId={activeCard.currentStage === 'PACKED' ? 'PACKED' : 'PACKING'} />
        )}

        <DragOverlay>
          {activeCard && (
            <div className="w-[280px] rotate-2 opacity-90">
              <OrderCard order={activeCard} onAction={() => {}} />
            </div>
          )}
        </DragOverlay>
      </DndContext>

      {/* Delivery assign popup */}
      {assignTarget && (
        <PackingAssignPopup
          order={assignTarget}
          groups={assignTarget.currentStage === 'PACKED' ? packedGroups : packingGroups}
          onAssign={handleAssignFromPopup}
          onClose={() => setAssignTarget(null)}
        />
      )}
    </div>
  );
}
