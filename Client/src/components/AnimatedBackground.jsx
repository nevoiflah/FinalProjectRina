import { useEffect, useRef } from 'react';

const AnimatedBackground = () => {
    const canvasRef = useRef(null);

    useEffect(() => {
        const canvas = canvasRef.current;
        const ctx = canvas.getContext('2d');

        let animationFrameId;
        let particles = [];
        let centerX, centerY, scale;
        const reducedMotionQuery = window.matchMedia('(prefers-reduced-motion: reduce)');

        const resize = () => {
            canvas.width = window.innerWidth;
            canvas.height = window.innerHeight;

            // Anchor the tree further perfectly to the left flank, away from the central chat UI
            centerX = canvas.width * 0.18;
            // Lower the tree while keeping its newly established proportions
            centerY = canvas.height * 0.42;

            // Reduce scale slightly so it fits comfortably on the side without clipping
            scale = Math.min(canvas.width, canvas.height) * 0.45 / 5;

            initParticles();
        };

        // Strict 15-dot layout approximating the actual Ruppin tree logo geometry
        const treeLayout = [
            { x: 0, y: -2.5 },
            { x: -1, y: -2 }, { x: 1, y: -2 },
            { x: -2, y: -1 }, { x: 0, y: -1 }, { x: 2, y: -1 },
            { x: -2.5, y: 0 }, { x: -1, y: 0 }, { x: 1, y: 0 }, { x: 2.5, y: 0 },
            { x: -2, y: 1 }, { x: 0, y: 1 }, { x: 2, y: 1 },
            { x: -1, y: 2 }, { x: 1, y: 2 }
        ];

        class Particle {
            constructor(targetX, targetY, scale) {
                this.baseX = targetX;
                this.baseY = targetY;

                // Base size matching the thick, bold dots of the logo
                this.baseSize = scale * 0.45;
                this.size = this.baseSize;

                // Each dot pulses slightly out of sync to make the tree look alive
                this.pulseSpeed = Math.random() * 0.03 + 0.01;
                this.pulseAngle = Math.random() * Math.PI * 2;

                // Very tiny positional drift, keeping strict strict logo shape intact
                this.driftAngle = Math.random() * Math.PI * 2;
                this.driftSpeed = Math.random() * 0.02;
                this.driftRadius = scale * 0.08;
            }

            draw() {
                // Breathing / Pulsing effect
                this.pulseAngle += this.pulseSpeed;
                this.size = this.baseSize + Math.sin(this.pulseAngle) * (this.baseSize * 0.15); // +/- 15% pulse

                // Tiny structural drift
                this.driftAngle += this.driftSpeed;
                const x = this.baseX + Math.cos(this.driftAngle) * this.driftRadius;
                const y = this.baseY + Math.sin(this.driftAngle) * this.driftRadius;

                ctx.beginPath();
                ctx.arc(x, y, this.size, 0, Math.PI * 2);
                ctx.fillStyle = '#ffffff'; // Solid white
                ctx.fill();
                ctx.closePath();
            }
        }

        const initParticles = () => {
            particles = [];
            treeLayout.forEach(pos => {
                const targetX = centerX + (pos.x * scale);
                // Shift the canopy slightly up from the center
                const targetY = centerY - (scale * 0.5) + (pos.y * scale);
                particles.push(new Particle(targetX, targetY, scale));
            });
        };

        const drawLogoGeometry = () => {
            const width = canvas.width;
            const height = canvas.height;

            const startY = height * 0.82;
            const control1X = width * 0.3;
            const control1Y = height * 0.95;
            const control2X = width * 0.7;
            const control2Y = height * 0.25;
            const endY = height * 0.40;

            // 1. Fill entire background with exact Logo Green
            ctx.fillStyle = '#79bf59';
            ctx.fillRect(0, 0, width, height);

            // 2. Draw the White Stem sweeping infinitely downwards
            // It will be perfectly masked at the bottom by the White Wave Stroke and Blue Ocean Fill
            ctx.beginPath();
            const topY = centerY + (1.5 * scale);
            const stemWidthTop = scale * 0.4;

            ctx.moveTo(centerX - stemWidthTop, topY);

            // Left curve plunges deep and left into the hidden zone
            ctx.quadraticCurveTo(
                centerX - (scale * 0.5), topY + (scale * 1.5),
                centerX - (scale * 4), startY + (height * 0.2)
            );

            // Straight across the bottom (will be completely hidden)
            ctx.lineTo(centerX + (scale * 5), startY + (height * 0.2));

            // Right curve sweeping from deep right back up to top right of the trunk
            // This huge outward sweep natively forms the ultra-sharp point when clipped by the wave
            ctx.quadraticCurveTo(
                centerX + (scale * 0.5), topY + (scale * 1.5),
                centerX + stemWidthTop, topY
            );

            ctx.fillStyle = '#ffffff';
            ctx.fill();
            ctx.closePath();

            // 3. Draw the thick white sweeping stroke
            // We draw it 2x thicker because the Blue fill will seamlessly cover its exact bottom half
            const waveThickness = Math.max(width * 0.015, 12);
            ctx.beginPath();
            ctx.moveTo(0, startY);
            ctx.bezierCurveTo(control1X, control1Y, control2X, control2Y, width, endY);
            ctx.lineWidth = waveThickness * 2;
            ctx.strokeStyle = '#ffffff';
            ctx.stroke();
            ctx.closePath();

            // 4. Draw the sweeping wave and fill below it with exact Logo Blue
            ctx.beginPath();
            ctx.moveTo(0, startY);
            ctx.bezierCurveTo(control1X, control1Y, control2X, control2Y, width, endY);
            ctx.lineTo(width, height);
            ctx.lineTo(0, height);
            ctx.fillStyle = '#2d6795'; // Logo Blue
            ctx.fill();
            ctx.closePath();

            // A soft wash keeps the brand shape visible without fighting the UI panels.
            ctx.fillStyle = 'rgba(255, 255, 255, 0.08)';
            ctx.fillRect(0, 0, width, height);
        };

        const animate = () => {
            ctx.clearRect(0, 0, canvas.width, canvas.height);

            drawLogoGeometry();

            // Draw the canopy dots on top of the stem
            particles.forEach(particle => {
                particle.draw();
            });

            if (!reducedMotionQuery.matches) {
                animationFrameId = requestAnimationFrame(animate);
            }
        };

        const handleVisibilityChange = () => {
            if (document.hidden) {
                cancelAnimationFrame(animationFrameId);
            } else if (!reducedMotionQuery.matches) {
                animationFrameId = requestAnimationFrame(animate);
            }
        };

        window.addEventListener('resize', resize);
        document.addEventListener('visibilitychange', handleVisibilityChange);
        resize();
        animate();

        return () => {
            window.removeEventListener('resize', resize);
            document.removeEventListener('visibilitychange', handleVisibilityChange);
            cancelAnimationFrame(animationFrameId);
        };
    }, []);

    return (
        <canvas
            ref={canvasRef}
            aria-hidden="true"
            style={{
                position: 'fixed',
                top: 0,
                left: 0,
                width: '100%',
                height: '100%',
                zIndex: 0,
                pointerEvents: 'none',
                opacity: 1
            }}
        />
    );
};

export default AnimatedBackground;
