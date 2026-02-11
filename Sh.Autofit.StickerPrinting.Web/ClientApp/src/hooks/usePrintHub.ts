import { useCallback, useEffect, useRef, useState } from 'react';
import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr';
import type { PrintProgress, PrintComplete } from '../types';

export function usePrintHub() {
  const connectionRef = useRef<HubConnection | null>(null);
  const [progress, setProgress] = useState<PrintProgress | null>(null);
  const [isPrinting, setIsPrinting] = useState(false);
  const [result, setResult] = useState<PrintComplete | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/print')
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on('PrintProgress', (data: PrintProgress) => {
      setProgress(data);
    });

    connection.on('PrintComplete', (data: PrintComplete) => {
      setResult(data);
      setIsPrinting(false);
      setProgress(null);
    });

    connection.on('PrintError', (data: { itemKey: string; error: string }) => {
      setError(`Error printing ${data.itemKey}: ${data.error}`);
    });

    connection.start().catch(err => {
      console.error('SignalR connection failed:', err);
    });

    connectionRef.current = connection;

    return () => {
      connection.stop();
    };
  }, []);

  const printBatch = useCallback(
    async (items: { itemKey: string; language?: string; quantity: number }[], printerName: string, defaultLanguage?: string) => {
      const connection = connectionRef.current;
      if (!connection) return;

      setIsPrinting(true);
      setProgress(null);
      setResult(null);
      setError(null);

      try {
        await connection.invoke('PrintBatch', {
          items,
          printerName,
          defaultLanguage: defaultLanguage ?? 'he',
        });
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Unknown error');
        setIsPrinting(false);
      }
    },
    [],
  );

  const reset = useCallback(() => {
    setProgress(null);
    setResult(null);
    setError(null);
    setIsPrinting(false);
  }, []);

  return { printBatch, progress, isPrinting, result, error, reset };
}
