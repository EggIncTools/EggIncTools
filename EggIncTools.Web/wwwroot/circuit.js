let leaving = false;
let pending = 0;
const markLeaving = () => leaving = true;
addEventListener("pagehide", markLeaving);
addEventListener("beforeunload", markLeaving);

Blazor.start({
    circuit: {
        reconnectionHandler: {
            onConnectionDown: () => {
                clearTimeout(pending);
                pending = setTimeout(() => {
                    if (leaving || document.visibilityState === "hidden") return;
                    if (sessionStorage.getItem("eit-circuit-retry") === "1") return;
                    sessionStorage.setItem("eit-circuit-retry", "1");
                    location.reload();
                }, 500);
            },
            onConnectionUp: () => {
                clearTimeout(pending);
                sessionStorage.removeItem("eit-circuit-retry");
            },
        },
    },
});
