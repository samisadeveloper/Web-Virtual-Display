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
  const currentSdpRef = useRef<string | null>(null);

  const [streams, setStreams] = useState<MediaStream[]>([]);
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

    peerConnection.current.ontrack = (event) => {
            const incomingStreams = [...event.streams]; 

            if (incomingStreams.length === 0) return;

            const targetStream = incomingStreams[incomingStreams.length - 1];

            console.log("incoming stream: ", targetStream.id);

            setStreams((prev) => {
                    // If we already have the stream ID, don't change anything in state.
                    // The browser is already modifying the tracks inside the object natively.
                    if (prev.some(s => s.id === targetStream.id)) return prev;
                    return [...prev, targetStream];
            });
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

    const executeNegotiation = async () => {
      try {
        const res = await fetch('/api/webrtc/offer');
        const offerSdp = await res.text();

        if (!offerSdp || !peerConnection.current) return;
        
        // Skip execution if the SDP offer hasn't actually updated
        if (offerSdp === currentSdpRef.current) return;
        currentSdpRef.current = offerSdp;

        await peerConnection.current.setRemoteDescription(
          new RTCSessionDescription({ type: 'offer', sdp: offerSdp })
        );

        const answer = await peerConnection.current.createAnswer();
        await peerConnection.current.setLocalDescription(answer);

        console.log("Renegotiatied with the C# host!");

        await fetch('/api/webrtc/answer', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ sdp: answer.sdp, type: answer.type }),
        });
      } catch (err) {
        console.error("Negotiation update failed:", err);
      }
    };

    const negotiationPoll = setInterval(() => {
       executeNegotiation();
    }, 1000);

    // Run initial connection handshake on mount
    setStatus('Fetching offer from C# host...');
    executeNegotiation().then(() => setStatus('Handshake sent. Finalizing local connection...'));

    return () => {
      clearInterval(iceInterval);
      clearInterval(negotiationPoll);
      if (dataChannel.current) dataChannel.current.close();
      if (peerConnection.current) peerConnection.current.close();
    };
  }, []);

  return { status, streams };
}
