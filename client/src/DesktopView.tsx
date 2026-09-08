import { useState, type CSSProperties, type ReactNode } from "react";
import { useWebRTCConnection } from "./hooks/UseWebRTCConnection";

import { FaMousePointer } from "react-icons/fa";

type Component = {
        channel: string;
        x: number;
        y: number;
        width?: number;
        height?: number;
};

function renderComponent(component: Component) {
        const style = {position: 'absolute', top: component.y, left: component.x} as CSSProperties;

        // TODO: replace with a lookup instead
        if (component.channel === 'mouse') {
                return (
                        <FaMousePointer style={style} />
                );
        }
}

export default function DesktopView() {
        const [components, setComponents] = useState<ReactNode>();

        const { status } = useWebRTCConnection((data) => {
                const componentData = JSON.parse(data) as Component;

                if (componentData) {
                        setComponents(renderComponent(componentData));
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
                        {components}
                </div>
        );
}
