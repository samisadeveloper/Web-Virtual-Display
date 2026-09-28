import { useEffect, useRef, useState, type CSSProperties, type ReactNode } from "react";
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

function Window({component, style, media}: {component: Component, style: CSSProperties, media: MediaStream | null}) {
        const videoRef = useRef<HTMLVideoElement>(null);

        useEffect(() => {
                if (media) {
                        if (videoRef.current) videoRef.current.srcObject = media;
                }
        }, [media, videoRef.current]);

        return (
                <video
                        autoPlay
                        playsInline
                        width={component.width}
                        height={component.height}
                        muted
                        ref={videoRef} 
                        style={{...style, borderWidth: 1}} 
                />
        );
}

function renderComponent(component: Component, stream: MediaStream | null) : ReactNode {
        let style = {position: 'absolute', top: component.y, left: component.x} as CSSProperties;

        // append the width and height if applicable
        if (component.width) style = {width: component.width, ...style};
        if (component.height) style = {height: component.height, ...style};

        const renderers: Record<string, () => ReactNode> = {
                mouse: () => <FaMousePointer style={style} />,
                window: () => <Window component={component} style={style} media={stream} />
        };

        const rendered = renderers[component.channel]?.();

        return rendered;
}

function getStream(streams: Record<string, MediaStream>, component: Component) : MediaStream | null {
        if ((component.id) == -1) return null;

        const stream = streams[component.id];

        return stream;
}

export default function DesktopView() {
        const [componentConfigs, setComponentConfigs] = useState<Record<string, Component>>({});

        const { status, streams } = useWebRTCConnection((data) => {
                const componentData = JSON.parse(data) as Component;
                if (componentData) {
                        setComponentConfigs(prevComponents => ({
                                ...prevComponents,
                                [componentData.id]: componentData
                        }));
                }
        });

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
                        {Object.values(componentConfigs).map((config) => {
                                const stream = getStream(streams, config);

                                const renderedComponent = renderComponent(config, stream);

                                return (
                                        <div>
                                                {renderedComponent}
                                        </div>
                                );
                        })}
                </div>
        );
}
