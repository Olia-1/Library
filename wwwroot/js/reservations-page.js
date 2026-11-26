document.addEventListener("DOMContentLoaded", () => {
    const list = document.getElementById("reservationList");
    if (!list) return;

    const cards = () => Array.from(list.querySelectorAll("[data-reservation-card]"));
    const filterButtons = Array.from(document.querySelectorAll("[data-reservation-filter]"));
    const search = document.getElementById("reservationSearch");
    const sort = document.getElementById("reservationSort");
    let activeFilter = "active";

    const categoryFor = status => {
        if (status === "pending" || status === "ready") return "active";
        if (status === "expired" || status === "canceled") return "history";
        return "other";
    };

    const updateCounts = () => {
        const all = cards();
        const counts = {
            active: all.filter(card => ["pending", "ready"].includes(card.dataset.status)).length,
            ready: all.filter(card => card.dataset.status === "ready").length,
            waiting: all.filter(card => card.dataset.status === "pending").length,
            history: all.filter(card => ["expired", "canceled", "completed"].includes(card.dataset.status)).length
        };
        Object.entries(counts).forEach(([key, value]) => {
            const target = document.querySelector(`[data-count="${key}"]`);
            if (target) target.textContent = String(value);
        });
    };

    const apply = () => {
        const query = (search?.value || "").trim().toLocaleLowerCase();
        const ordered = cards();
        const mode = sort?.value || "newest";
        ordered.sort((a, b) => {
            if (mode === "oldest") return Number(a.dataset.date) - Number(b.dataset.date);
            if (mode === "title") return (a.dataset.title || "").localeCompare(b.dataset.title || "");
            if (mode === "deadline") return (Number(a.dataset.deadline) || Number.MAX_SAFE_INTEGER) - (Number(b.dataset.deadline) || Number.MAX_SAFE_INTEGER);
            return Number(b.dataset.date) - Number(a.dataset.date);
        }).forEach(card => list.appendChild(card));

        let visible = 0;
        cards().forEach(card => {
            const status = card.dataset.status;
            const matchesFilter = activeFilter === "all"
                || (activeFilter === "active" && ["pending", "ready"].includes(status))
                || (activeFilter === "ready" && status === "ready")
                || (activeFilter === "waiting" && status === "pending")
                || (activeFilter === "history" && ["expired", "canceled", "completed"].includes(status));
            const matchesSearch = (card.dataset.title || "").toLocaleLowerCase().includes(query);
            card.hidden = !(matchesFilter && matchesSearch);
            if (!card.hidden) visible++;
        });

        let empty = list.querySelector("[data-filter-empty]");
        if (!empty) {
            empty = document.createElement("p");
            empty.dataset.filterEmpty = "true";
            empty.className = "reader-reservation-filter-empty";
            empty.textContent = "No reservations match your filters.";
            list.appendChild(empty);
        }
        empty.hidden = visible !== 0;
    };

    filterButtons.forEach(button => button.addEventListener("click", () => {
        activeFilter = button.dataset.reservationFilter;
        filterButtons.forEach(item => {
            const selected = item === button;
            item.classList.toggle("is-selected", selected);
            item.setAttribute("aria-pressed", String(selected));
        });
        apply();
    }));
    search?.addEventListener("input", apply);

    const sortDropdown = document.querySelector("[data-sort-dropdown]");
    const sortTrigger = document.getElementById("reservationSortTrigger");
    const sortMenu = document.getElementById("reservationSortMenu");
    const sortCurrent = sortDropdown?.querySelector("[data-sort-current]");
    const sortOptions = Array.from(sortDropdown?.querySelectorAll("[data-sort-option]") || []);
    const sortLabels = {
        newest: "Newest first",
        oldest: "Oldest first",
        title: "Book title",
        deadline: "Collection deadline"
    };

    const closeSortMenu = () => {
        sortDropdown?.classList.remove("is-open");
        sortTrigger?.setAttribute("aria-expanded", "false");
    };
    const syncSortMenu = () => {
        const value = sort?.value || "newest";
        if (sortCurrent) sortCurrent.textContent = sortLabels[value] || sortLabels.newest;
        sortOptions.forEach(option => {
            const selected = option.dataset.sortOption === value;
            option.classList.toggle("is-selected", selected);
            option.setAttribute("aria-selected", String(selected));
        });
    };

    sortTrigger?.addEventListener("click", () => {
        const opening = !sortDropdown.classList.contains("is-open");
        sortDropdown.classList.toggle("is-open", opening);
        sortTrigger.setAttribute("aria-expanded", String(opening));
    });
    sortOptions.forEach(option => option.addEventListener("click", () => {
        if (sort) {
            sort.value = option.dataset.sortOption;
            sort.dispatchEvent(new Event("change", { bubbles: true }));
        }
        syncSortMenu();
        closeSortMenu();
    }));
    document.addEventListener("click", event => {
        if (sortDropdown && !sortDropdown.contains(event.target)) closeSortMenu();
    });
    sortTrigger?.addEventListener("keydown", event => {
        if (event.key === "Escape") closeSortMenu();
    });
    sort?.addEventListener("change", () => {
        syncSortMenu();
        apply();
    });
    syncSortMenu();

    list.addEventListener("submit", async event => {
        const form = event.target.closest("form");
        if (!form || !form.querySelector(".reader-reservation-cancel")) return;
        event.preventDefault();
        const button = form.querySelector(".reader-reservation-cancel");
        button.disabled = true;
        button.textContent = "Canceling…";
        try {
            const response = await fetch(form.action, {
                method: "POST",
                body: new FormData(form),
                headers: { "X-Requested-With": "XMLHttpRequest", "Accept": "application/json" }
            });
            const result = await response.json();
            if (!response.ok || !result.success) throw new Error(result.message || "Could not cancel this reservation.");

            const card = form.closest("[data-reservation-card]");
            if (card) {
                card.dataset.status = "canceled";
                const status = card.querySelector(".reader-reservation-status");
                if (status) {
                    status.className = "reader-reservation-status is-canceled";
                    status.textContent = "Canceled";
                }
                const details = card.querySelector(".reader-reservation-details");
                if (details) {
                    const last = details.lastElementChild;
                    if (last) last.innerHTML = "<span>Reservation</span><strong>Canceled</strong>";
                }
                card.querySelector(".reader-reservation-deadline")?.remove();
                card.querySelector(".reader-reservation-actions")?.remove();
            }
            updateCounts();
            apply();
        } catch (error) {
            button.disabled = false;
            button.textContent = "Cancel";
            let message = document.querySelector(".reader-reservation-message-error[data-reservation-error]");
            if (!message) {
                message = document.createElement("div");
                message.className = "reader-reservation-message reader-reservation-message-error";
                message.dataset.reservationError = "true";
                message.setAttribute("role", "alert");
                list.parentElement.insertBefore(message, list);
            }
            message.textContent = error.message || "Something went wrong. Please try again.";
        }
    });

    updateCounts();
    apply();
});
