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

function renderComponent(component: Component) : ReactNode {
        let style = {position: 'absolute', top: component.y, left: component.x} as CSSProperties;

        // append the width and height if applicable
        if (component.width) style = {width: component.width, ...style};
        if (component.height) style = {height: component.height, ...style};

        const renderers: Record<string, () => ReactNode> = {
                mouse: () => <FaMousePointer style={style} />,
                window: () => <div style={{backgroundColor: 'white', ...style}}>This is a window {component.x}, {component.y}</div>
        };

        const rendered = renderers[component.channel]?.();

        return rendered;
}

export default function DesktopView() {
        const [components, setComponents] = useState<Record<string, ReactNode>>({});

        const { status } = useWebRTCConnection((data) => {
                const componentData = JSON.parse(data) as Component;

                if (componentData) {
                        const renderered = renderComponent(componentData);

                        console.log("rendered a component", componentData);

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
