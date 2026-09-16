Blazor.start({
    circuit: {
        reconnectionHandler: {
            onConnectionDown: () => {
                if (sessionStorage.getItem("eit-circuit-retry") === "1") return;
                sessionStorage.setItem("eit-circuit-retry", "1");
                location.reload();
            },
            onConnectionUp: () => sessionStorage.removeItem("eit-circuit-retry"),
        },
    },
});
