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
  const peerConnection = useRef<RTCPeerConnection | null>(null);
  const dataChannel = useRef<RTCDataChannel | null>(null);
  const [status, setStatus] = useState<WebRTCStatus>('Idle');

  useEffect(() => {
    peerConnection.current = new RTCPeerConnection({
      iceServers: [] 
    });

    peerConnection.current.onicecandidate = (event) => {
      if (event.candidate) {
        fetch('/api/webrtc/ice', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(event.candidate.toJSON()),
        }).catch(err => console.error("Failed to send local ICE:", err));
      }
    };

    peerConnection.current.ondatachannel = (event) => {
      dataChannel.current = event.channel;

      dataChannel.current.onopen = () => {
        setStatus('Connected');
      };

      dataChannel.current.onmessage = (msgEvent) => {
        // Pass data up to your component if a callback was provided
        if (onDataReceived) {
          onDataReceived(msgEvent.data);
        }
      };

      dataChannel.current.onclose = () => {
        setStatus('Disconnected');
      };
    };

    const iceInterval = setInterval(async () => {
      if (!peerConnection.current || !peerConnection.current.remoteDescription) return;
      
      try {
        const res = await fetch('/api/webrtc/ice');
        if (!res.ok) return;
        const candidates = await res.json();
        
        candidates.forEach((cand: any) => {
          if (cand && peerConnection.current) {
            peerConnection.current
              .addIceCandidate(new RTCIceCandidate({ candidate: cand, sdpMLineIndex: 0 }))
              .catch(() => {});
          }
        });
      } catch (err) {
        console.error("ICE polling error:", err);
      }
    }, 1500);

    const startHandshake = async () => {
      try {
        setStatus('Fetching offer from C# host...');
        const res = await fetch('/api/webrtc/offer');
        const offerSdp = await res.text();

        if (!offerSdp || !peerConnection.current) {
          setStatus('Error: C# host has not generated an offer yet.');
          return;
        }

        await peerConnection.current.setRemoteDescription(
          new RTCSessionDescription({ type: 'offer', sdp: offerSdp })
        );

        const answer = await peerConnection.current.createAnswer();
        await peerConnection.current.setLocalDescription(answer);

        setStatus('Sending answer back to C#...');

        await fetch('/api/webrtc/answer', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ sdp: answer.sdp, type: answer.type }),
        });

        setStatus('Handshake sent. Finalizing local connection...');
      } catch (err) {
        console.error(err);
        setStatus('Connection failed.');
      }
    };

    startHandshake();

    return () => {
      clearInterval(iceInterval);
      if (dataChannel.current) dataChannel.current.close();
      if (peerConnection.current) peerConnection.current.close();
    };
  }, []);

  return { status };
}
