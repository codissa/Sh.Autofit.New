import type { RefObject } from 'react';

interface Props {
  containerRef: RefObject<HTMLDivElement | null>;
  onInteraction: () => void;
}

export default function ScrollButtons({ containerRef, onInteraction }: Props) {
  const scrollStep = 200;

  const scrollUp = () => {
    containerRef.current?.scrollBy({ top: -scrollStep, behavior: 'smooth' });
    onInteraction();
  };

  const scrollDown = () => {
    containerRef.current?.scrollBy({ top: scrollStep, behavior: 'smooth' });
    onInteraction();
  };

  return (
    <>
      <button
        onClick={scrollUp}
        onPointerDown={(e) => e.stopPropagation()}
        className="absolute top-0 left-1/2 -translate-x-1/2 z-10
          w-14 h-10 bg-black/30 hover:bg-black/50 active:bg-black/70
          text-white text-2xl rounded-b-xl backdrop-blur-sm
          flex items-center justify-center transition-colors"
        title="גלול למעלה"
      >
        ▲
      </button>
      <button
        onClick={scrollDown}
        onPointerDown={(e) => e.stopPropagation()}
        className="absolute bottom-0 left-1/2 -translate-x-1/2 z-10
          w-14 h-10 bg-black/30 hover:bg-black/50 active:bg-black/70
          text-white text-2xl rounded-t-xl backdrop-blur-sm
          flex items-center justify-center transition-colors"
        title="גלול למטה"
      >
        ▼
      </button>
    </>
  );
}
