import { useEffect, useRef, useState, type CSSProperties, type ReactNode, type Ref } from "react";
import { useWebRTCConnection } from "../hooks/UseWebRTCConnection";

import { FaMousePointer } from "react-icons/fa";

type Component = {
        channel: string;
        id: number;
        x: number;
        y: number;
        width?: number;
        height?: number;
};

function renderComponent(component: Component, videoRef: Ref<HTMLVideoElement>) : ReactNode {
        let style = {position: 'absolute', top: component.y, left: component.x} as CSSProperties;

        // append the width and height if applicable
        if (component.width) style = {width: component.width, ...style};
        if (component.height) style = {height: component.height, ...style};

        const renderers: Record<string, () => ReactNode> = {
                mouse: () => <FaMousePointer style={style} />,
                window: () => <video
                        autoPlay
                        playsInline
                        width={component.width}
                        height={component.height}
                        muted
                        ref={videoRef} 
                        style={{...style, borderWidth: 1}} 
                />
        };

        const rendered = renderers[component.channel]?.();

        return rendered;
}

export default function DesktopView() {
        const videoRef = useRef<HTMLVideoElement>(null);

        const [components, setComponents] = useState<Record<string, ReactNode>>({});

        const { status, streams } = useWebRTCConnection((data) => {
                const componentData = JSON.parse(data) as Component;

                if (componentData) {
                        // TODO: get the video reference of the component, not hardcoded.
                        const renderered = renderComponent(componentData, videoRef);

                        setComponents(prevComponents => {
                                return {
                                        ...prevComponents,
                                        [componentData.id]: renderered
                                }
                        });
                }
        });

        // TODO: get stream by HWND instead of hard coding the first stream
        useEffect(() => {
                if (streams[0]) {
                        console.log("we have at least one stream, setting our ref if possible");
                        if (!videoRef.current) {
                                console.log("unable to set our ref :(");
                        } else {
                                console.log("our ref exists lets set it to the stream");
                        }
                        if (videoRef.current) videoRef.current.srcObject = streams[0];
                }
        }, [streams, videoRef.current]);
        
        if (status != 'Connected') {
                return (
                        <div>
                                <h1>Still connecting...</h1>
                                <p>{status}</p>
                        </div>
                );
        }

        return (
                <div>
                        {Object.entries(components).map(([id, component]) => (
                                <div key={id}>{component}</div>
                        ))}
                </div>
        );
}
