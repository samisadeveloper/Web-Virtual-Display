import { useEffect, useState, useRef } from 'react';

export type WebRTCStatus =
  | 'Idle'
  | 'Fetching offer from C# host...'
  | 'Sending answer back to C#...'
  | 'Handshake sent. Finalizing local connection...'
  | 'Connected'
  | 'Disconnected'
  | 'Error: C# host has not generated an offer yet.'
  | 'Connection failed.';

export function useWebRTCConnection(onDataReceived?: (data: any) => void) {
  const [streams, setStreams] = useState<Record<string, MediaStream>>({});
  const [status, setStatus] = useState<WebRTCStatus>('Idle');

  // Keep the latest callback without re-running the effect
  const onDataRef = useRef(onDataReceived);
  onDataRef.current = onDataReceived;

  useEffect(() => {
    let cancelled = false;
    let iceInterval: ReturnType<typeof setInterval> | undefined;
    let negotiationPoll: ReturnType<typeof setInterval> | undefined;
    let pc: RTCPeerConnection | null = null;
    let dc: RTCDataChannel | null = null;

    let currentSdp: string | null = null;
    let negotiating = false;
    const addedCandidates = new Set<string>();

    const executeNegotiation = async () => {
      if (cancelled || !pc || negotiating) return;
      negotiating = true;
      try {
        const res = await fetch('/api/webrtc/offer');
        if (cancelled || !pc) return;

        if (!res.ok) {
          if (!currentSdp) setStatus('Error: C# host has not generated an offer yet.');
          return;
        }

        const offerSdp = await res.text();
        if (!offerSdp || offerSdp === currentSdp) return;
        currentSdp = offerSdp;

        if (dc?.readyState !== 'open') setStatus('Sending answer back to C#...');

        await pc.setRemoteDescription({ type: 'offer', sdp: offerSdp });
        const answer = await pc.createAnswer();
        await pc.setLocalDescription(answer);

        const ans = await fetch('/api/webrtc/answer', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ sdp: answer.sdp, type: answer.type }),
        });
        if (!ans.ok) throw new Error(`Answer rejected: ${ans.status}`);

        console.log('Negotiated with the C# host!');
        if (!cancelled) {
          setStatus(dc?.readyState === 'open'
            ? 'Connected'
            : 'Handshake sent. Finalizing local connection...');
        }
        // if (!cancelled) setStatus('Handshake sent. Finalizing local connection...');
      } catch (err) {
        console.error('Negotiation failed:', err);
        currentSdp = null; // allow a retry on the next poll
      } finally {
        negotiating = false;
      }
    };

    const pollIce = async () => {
      if (cancelled || !pc || !pc.remoteDescription) return;
      try {
        const res = await fetch('/api/webrtc/ice');
        if (!res.ok) return;
        const candidates: string[] = await res.json();

        for (const cand of candidates) {
          if (!cand || addedCandidates.has(cand)) continue;
          addedCandidates.add(cand);
          pc.addIceCandidate({ candidate: cand, sdpMLineIndex: 0 }).catch(() => {});
        }
      } catch (err) {
        console.error('ICE polling error:', err);
      }
    };

    const start = async () => {
      setStatus('Fetching offer from C# host...');

      // Tell the server to throw away any previous session and make a fresh offer
      try {
        await fetch('/api/webrtc/reset', { method: 'POST' });
      } catch (err) {
        console.error('Reset failed:', err);
      }
      if (cancelled) return;

      pc = new RTCPeerConnection({ iceServers: [] });

      pc.onicecandidate = (event) => {
        if (!event.candidate) return;
        fetch('/api/webrtc/ice', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(event.candidate.toJSON()),
        }).catch((err) => console.error('Failed to send local ICE:', err));
      };

      pc.onconnectionstatechange = () => {
        if (cancelled || !pc) return;
        if (pc.connectionState === 'failed') setStatus('Connection failed.');
        else if (pc.connectionState === 'disconnected') setStatus('Disconnected');
      };

      pc.ontrack = (event) => {
        const targetStream = event.streams[event.streams.length - 1];
        if (!targetStream) return;

        console.log('incoming stream: ', targetStream.id);

        setStreams((prev) => {
          // Same stream already tracked: return the same reference so React skips the update
          if (prev[targetStream.id]) return prev;
          return { ...prev, [targetStream.id]: targetStream };
        });
      };

      pc.ondatachannel = (event) => {
        dc = event.channel;
        dc.onopen = () => setStatus('Connected');
        dc.onmessage = (msg) => onDataRef.current?.(msg.data);
        dc.onclose = () => {
          if (!cancelled) setStatus('Disconnected');
        };
      };

      iceInterval = setInterval(pollIce, 1500);
      negotiationPoll = setInterval(executeNegotiation, 1000);
      executeNegotiation();
    };

    start();

    return () => {
      cancelled = true;
      clearInterval(iceInterval);
      clearInterval(negotiationPoll);
      dc?.close();
      pc?.close();
    };
  }, []);

  return { status, streams };
}
