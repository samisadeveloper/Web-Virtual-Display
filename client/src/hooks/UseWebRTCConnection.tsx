import { useEffect, useState, useRef } from 'react';

export type WebRTCStatus = 
  | 'Idle' 
  | 'Fetching offer from C# host...' 
  | 'Sending answer back to C#...' 
  | 'Handshake sent. Finalizing local connection...' 
  | 'Connected! Receiving data from C#...' 
  | 'Disconnected' 
  | 'Error: C# host has not generated an offer yet.' 
  | 'Connection failed.';

export function useWebRTCConnection(onDataReceived?: (data: any) => void) {
  const peerConnection = useRef<RTCPeerConnection | null>(null);
  const dataChannel = useRef<RTCDataChannel | null>(null);
  const [status, setStatus] = useState<WebRTCStatus>('Idle');

  useEffect(() => {
    // 1. Empty iceServers array bypasses STUN/TURN for purely local environments
    peerConnection.current = new RTCPeerConnection({
      iceServers: [] 
    });

    // 2. Post local ICE candidates to C# host
    peerConnection.current.onicecandidate = (event) => {
      if (event.candidate) {
        console.log("posting our ICE, the body looks like this\n", JSON.stringify(event.candidate.toJSON()));

        fetch('/api/webrtc/ice', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(event.candidate.toJSON()),
        }).catch(err => console.error("Failed to send local ICE:", err));
      }
    };

    // 3. Setup incoming data channel listener
    peerConnection.current.ondatachannel = (event) => {
      dataChannel.current = event.channel;

      dataChannel.current.onopen = () => {
        setStatus('Connected! Receiving data from C#...');
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

    // 4. Poll remote C# ICE candidates
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

    // 5. Run the negotiation handshake
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
        
        console.log(`posting our answer, the body looks like this \n ${JSON.stringify({sdp: answer.sdp, type: answer.type})}`);

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

    // Clean up connections on component unmount
    return () => {
      clearInterval(iceInterval);
      if (dataChannel.current) dataChannel.current.close();
      if (peerConnection.current) peerConnection.current.close();
    };
  }, []); // Empty dependency array ensures this runs strictly once on mount

  return { status };
}
