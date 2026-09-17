import { useState, type CSSProperties, type ReactNode } from "react";
import { useWebRTCConnection } from "./hooks/UseWebRTCConnection";

import { FaMousePointer } from "react-icons/fa";

type Component = {
        channel: string;
        id: number;
        x: number;
        y: number;
        width?: number;
        height?: number;
};

function MediaPlayer({ style, mediaStream }: { style: CSSProperties; mediaStream: MediaStream | null }) {
  // This callback fires whenever the element mounts or updates in the DOM
  const videoElementRef = (el: HTMLVideoElement | null) => {
    if (el && mediaStream) {
      // Just like the official docs: forcefully assign it to the DOM node directly
      if (el.srcObject !== mediaStream) {
        el.srcObject = mediaStream;
      }
    }
  };

  return (
    <video
      ref={videoElementRef} // 👈 Using the callback ref here
      autoPlay
      playsInline
      muted
      controls
      style={style}
    />
  );
}

function renderComponent(component: Component, mediaStream: MediaStream | null) : ReactNode {
        let style = {position: 'absolute', top: component.y, left: component.x} as CSSProperties;

        // append the width and height if applicable
        if (component.width) style = {width: component.width, ...style};
        if (component.height) style = {height: component.height, ...style};

        const renderers: Record<string, () => ReactNode> = {
                mouse: () => <FaMousePointer style={style} />,
                window: () => <MediaPlayer style={style} mediaStream={mediaStream}/>
                // window: () => <video autoPlay={true} src={mediaStream} style={{backgroundColor: 'white', ...style}}>This is a window {component.x}, {component.y}</video>
        };

        const rendered = renderers[component.channel]?.();

        return rendered;
}

export default function DesktopView() {
        const [components, setComponents] = useState<Record<string, ReactNode>>({});

        const { status, streams } = useWebRTCConnection((data) => {
                const componentData = JSON.parse(data) as Component;

                if (componentData) {
                        const renderered = renderComponent(componentData, streams[0]);

                        setComponents(prevComponents => {
                                return {
                                        ...prevComponents,
                                        [componentData.id]: renderered
                                }
                        });
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
                        {Object.entries(components).map(([id, component]) => (
                                <div key={id}>{component}</div>
                        ))}
                </div>
        );
}
