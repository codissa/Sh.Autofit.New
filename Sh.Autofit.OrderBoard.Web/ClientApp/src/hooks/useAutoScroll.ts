import { useEffect, useRef, useCallback, useState } from 'react';

export function useAutoScroll(speed = 0.3, idleDelayMs = 5000, bottomPauseMs = 2000) {
  const [container, setContainer] = useState<HTMLDivElement | null>(null);
  const animationRef = useRef<number | null>(null);
  const lastInteractionRef = useRef(Date.now());
  const phaseRef = useRef<'idle' | 'scrolling-down' | 'pause-bottom' | 'jump-top'>('idle');

  const setRef = useCallback((node: HTMLDivElement | null) => {
    setContainer(node);
  }, []);

  useEffect(() => {
    if (!container) return;

    const resetIdle = () => {
      lastInteractionRef.current = Date.now();
      phaseRef.current = 'idle';
    };

    // Any interaction resets the idle timer
    container.addEventListener('pointerdown', resetIdle);
    container.addEventListener('wheel', resetIdle, { passive: true });
    container.addEventListener('mouseenter', resetIdle);
    container.addEventListener('mouseleave', resetIdle);

    let pauseTimer: ReturnType<typeof setTimeout> | null = null;

    const animate = () => {
      if (!container || container.scrollHeight <= container.clientHeight) {
        animationRef.current = requestAnimationFrame(animate);
        return;
      }

      const now = Date.now();
      const maxScroll = container.scrollHeight - container.clientHeight;

      // Phase: idle — wait for idle delay since last interaction
      if (phaseRef.current === 'idle') {
        if (now - lastInteractionRef.current >= idleDelayMs) {
          phaseRef.current = 'scrolling-down';
        }
        animationRef.current = requestAnimationFrame(animate);
        return;
      }

      // Phase: scrolling down
      if (phaseRef.current === 'scrolling-down') {
        container.scrollTop += speed;
        if (container.scrollTop >= maxScroll) {
          container.scrollTop = maxScroll;
          phaseRef.current = 'pause-bottom';
          pauseTimer = setTimeout(() => {
            phaseRef.current = 'jump-top';
          }, bottomPauseMs);
        }
        animationRef.current = requestAnimationFrame(animate);
        return;
      }

      // Phase: pausing at bottom
      if (phaseRef.current === 'pause-bottom') {
        animationRef.current = requestAnimationFrame(animate);
        return;
      }

      // Phase: jump to top, then wait idle delay before restarting
      if (phaseRef.current === 'jump-top') {
        container.scrollTop = 0;
        phaseRef.current = 'idle';
        lastInteractionRef.current = now; // triggers idle wait
        animationRef.current = requestAnimationFrame(animate);
        return;
      }

      animationRef.current = requestAnimationFrame(animate);
    };

    animationRef.current = requestAnimationFrame(animate);

    return () => {
      container.removeEventListener('pointerdown', resetIdle);
      container.removeEventListener('wheel', resetIdle);
      container.removeEventListener('mouseenter', resetIdle);
      container.removeEventListener('mouseleave', resetIdle);
      if (animationRef.current) cancelAnimationFrame(animationRef.current);
      if (pauseTimer) clearTimeout(pauseTimer);
    };
  }, [container, speed, idleDelayMs, bottomPauseMs]);

  const resetIdle = useCallback(() => {
    lastInteractionRef.current = Date.now();
    phaseRef.current = 'idle';
  }, []);

  return { setRef, resetIdle };
}
