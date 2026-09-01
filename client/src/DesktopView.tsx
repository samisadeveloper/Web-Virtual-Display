import { useState } from "react";
import { useWebRTCConnection } from "./hooks/UseWebRTCConnection";

export default function DesktopView() {
        const [latestMessage, setLatestMessage] = useState<string>('');

        // Handle incoming stream updates directly
        const { status } = useWebRTCConnection((data) => {
                setLatestMessage(data); 
        });

        return (
                <div>
                <p>Status: {status}</p>
                <p>Latest Data: {latestMessage}</p>
                </div>
        );
}
