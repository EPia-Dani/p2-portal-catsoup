**# MEMORIA DEL PROYECTO – SISTEMA DE PORTALES EN UNITY**

---

## **1. Introducción**  
El objetivo del proyecto es desarrollar un sistema funcional de portales en Unity, inspirado en *Portal* de Valve, que combine renderizado recursivo, control preciso de cámara y animaciones visuales mediante shaders personalizados. El trabajo se ha centrado tanto en la parte técnica (recursividad, optimización, render pipeline) como en la estructuración limpia del código y su modularización.

---

## **2. Primera fase: Base del proyecto y control de jugador**  
Se partió del proyecto de muestra, al que se incorporaron los scripts de control FPS del proyecto anterior.  
En el proyecto anterior se usaba un sistema basado completamente en **eventos globales**, lo que complicaba el flujo del movimiento y de las acciones del jugador.  
En esta nueva versión, se realizó un **refactor completo**, unificando todo en un único script y eliminando la dependencia de eventos.  

El control se implementó directamente con el **Input System de Unity** exportado a C#, mediante la siguiente estructura:

```csharp
private void ReadInput(out Vector2 moveInput, out Vector2 lookInput, out bool jumpPressed) {
    moveInput = _controls.Player.Move.ReadValue<Vector2>();
    lookInput = _controls.Player.Look.ReadValue<Vector2>();
    jumpPressed = _controls.Player.Jump.WasPerformedThisFrame();
    _moveInput = moveInput;
}
```

Donde:
```csharp
private Input.PlayerInput _controls;
_controls = InputManager.PlayerInput;
```

Esto permite una lectura más directa, controlada y eficiente del input del jugador.

---

## **3. Segunda fase: Implementación inicial del portal**  
El primer paso fue una versión simple del portal que solo mostraba una **Render Texture**, sin recursividad ni interacción.  
Tras varias iteraciones y revisiones de código (asistidas por IA), se optimizó la estructura y se comenzó a trabajar en el **shader** del portal.  

El shader combina varias fuentes y técnicas encontradas en línea, junto con ruido procedural y curvas de animación para representar:
- Apertura y cierre del portal.  
- Estado cuando hay un solo portal activo.  
- Estado cuando ambos portales están enlazados.

El shader es un **WIP** (en desarrollo), pero logró un resultado visual fluido y flexible.

---

## **4. Tercera fase: Sistema de colocación (placement) del portal**  
Inicialmente, el *placement* se gestionaba dentro del `PortalRenderer`, pero se refactorizó a la **PortalGun** para centralizar la lógica.  

El sistema determina si el portal puede colocarse en una superficie comprobando los **bounds** del `BoxCollider`:
1. Se comparan los límites del portal con los del collider.  
2. Se comprueba si el portal intersecta con otro portal existente.  
3. Solo si no hay conflictos se permite la colocación.  

A pesar de funcionar correctamente, el sistema aún es **WIP** y se prevé simplificarlo y optimizarlo.

---

## **5. Cuarta fase: Recursividad del renderizado**  
El objetivo era conseguir el efecto de “portal dentro del portal”.  
En muchos ejemplos, se hace renderizando la cámara del portal varias veces, usando la textura resultante en la siguiente iteración.  

En este proyecto, en cambio, se calculan las **transformaciones de cámara** necesarias en cada nivel de recursión:
- Se generan tantas matrices de transformación como pasos de recursividad.  
- Se renderiza de forma recursiva, sin recalcular todo el pipeline en cada paso.  

Esto permitió una **reducción significativa del coste** computacional frente al renderizado iterativo clásico, aunque sigue siendo un punto de optimización pendiente.

---

## **6. Quinta fase: Compatibilidad con la build y framerate adaptativo**  
Durante las pruebas, el sistema funcionaba correctamente en el **Editor**, pero no en la **Build**.  
El problema provenía del uso de `camera.Render()` en el contexto del **Universal Render Pipeline (URP)**.  
La solución fue sustituirlo por:

```csharp
context.ExecuteCommandBuffer(GetCommandBuffer());
#pragma warning disable CS0618
UniversalRenderPipeline.RenderSingleCamera(context, portalCamera);
#pragma warning restore CS0618
```

Esto resolvió el renderizado en la build.  
El proceso fue complejo, ya que la causa no aparecía documentada claramente y requirió muchas horas de prueba e investigación, incluyendo múltiples iteraciones con diferentes IA.

Además, se reimplementó un **sistema de framerate variable** para los portales, generando las RenderTextures por código para garantizar compatibilidad y estabilidad.

---

## **7. Sexta fase: Modularización del sistema de portales**  
Debido al crecimiento del código y la necesidad de mantenimiento, se desglosó el sistema en varios scripts:
- **PortalRenderer:** gestiona el renderizado y la recursividad.  
- **PortalAnimation:** controla las animaciones de apertura, cierre y transición del shader.  
- **PortalGun:** gestiona el disparo, la detección de superficie y la colocación del portal.  
- **PortalCulling:** desactiva el render cuando el portal no es visible, mejorando rendimiento.  
- **PortalCameraManager:** controla las transformaciones de cámara y matrices en recursividad.

Esta separación facilita la depuración y el análisis de rendimiento, además de permitir futuras ampliaciones o refactors parciales sin romper la estructura global.

---

## **8. Estado actual y próximos pasos**  
Actualmente, el sistema de portales:
- Funciona correctamente tanto en editor como en build.  
- Permite recursividad configurable.  
- Posee shader animado funcional.  
- Gestiona la colocación con validaciones de colisión y solapamiento.  

Tareas pendientes:
- Limpieza y revisión de código.  
- Optimización del placement y de los cálculos de recursión.  
- Unificación de cálculos redundantes y posible simplificación del pipeline de renderizado.  
- Mejora del shader y la animación de apertura.  


## - Importante: Implementar la **teletransportación** entre portales, que es el siguiente gran hito del proyecto.

---

## **9. Conclusión**  
El desarrollo ha permitido entender en profundidad:
- El funcionamiento interno del **URP y las cámaras personalizadas**.  
- El uso del **Input System** con código generado en C#.  
- La importancia del **refactor y modularización** en proyectos complejos.  
- Las implicaciones de **render recursivo y gestión de rendimiento**.  

A pesar de los retos técnicos, el sistema de portales es estable, escalable y preparado para futuras mejoras visuales y de rendimiento.
