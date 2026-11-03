// Responsive navigation menu
document.addEventListener("DOMContentLoaded", () => {
    const toggle = document.querySelector(".nav-toggle");
    const nav = document.querySelector("#navLinks");
    if (toggle && nav) {
        toggle.addEventListener("click", () => {
            const isOpen = nav.classList.toggle("is-open");
            toggle.setAttribute("aria-expanded", String(isOpen));
        });
    }

    // Show/hide password fields on account pages
    document.querySelectorAll("[data-toggle-password]").forEach(button => {
        button.addEventListener("click", () => {
            const input = document.getElementById(button.dataset.togglePassword);
            if (!input) return;
            const show = input.type === "password";
            input.type = show ? "text" : "password";
            button.setAttribute("aria-label", show ? "Hide password" : "Show password");
            button.textContent = show ? "◎" : "◉";
        });
    });

    // Visual favorite toggle for the homepage mockup.
    // Persisting favorites should be implemented with the logged-in user's database record.
    document.querySelectorAll(".favorite-toggle").forEach(button => {
        button.addEventListener("click", () => {
            const active = button.getAttribute("aria-pressed") === "true";
            button.setAttribute("aria-pressed", String(!active));
            button.textContent = active ? "♡" : "♥";
        });
    });
});
