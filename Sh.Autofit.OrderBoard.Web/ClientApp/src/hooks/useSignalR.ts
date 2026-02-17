import { useEffect, useRef, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';

export function useSignalR(onBoardChanged: () => void) {
  const connectionRef = useRef<signalR.HubConnection | null>(null);
  const callbackRef = useRef(onBoardChanged);
  callbackRef.current = onBoardChanged;

  const stableCallback = useCallback(() => {
    callbackRef.current();
  }, []);

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/board')
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Information)
      .build();

    connection.on('board.diff', () => {
      stableCallback();
    });

    connection.on('order.updated', () => {
      stableCallback();
    });

    connection.on('delivery.updated', () => {
      stableCallback();
    });

    connection.onreconnected(() => {
      console.log('SignalR reconnected');
      stableCallback();
    });

    connection
      .start()
      .then(() => console.log('SignalR connected to /hubs/board'))
      .catch((err) => console.error('SignalR connection error:', err));

    connectionRef.current = connection;

    return () => {
      connection.stop();
    };
  }, [stableCallback]);

  return connectionRef;
}
