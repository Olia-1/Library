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

    // Branded confirmation dialog for important site actions.
    const confirmDialog = document.getElementById("siteConfirmDialog");
    const confirmTitle = document.getElementById("siteConfirmTitle");
    const confirmMessage = document.getElementById("siteConfirmMessage");
    const confirmAccept = document.getElementById("siteConfirmAccept");
    const confirmCancel = document.getElementById("siteConfirmCancel");
    let pendingConfirmedForm = null;

    document.querySelectorAll("form").forEach(form => {
        const confirmButton = form.querySelector("[data-site-confirm]");
        if (!confirmButton || !confirmDialog) return;

        confirmButton.addEventListener("click", event => {
            event.preventDefault();
            pendingConfirmedForm = form;
            if (confirmTitle) {
                confirmTitle.textContent = confirmButton.dataset.siteConfirmTitle || "Please confirm";
            }
            if (confirmMessage) {
                confirmMessage.textContent = confirmButton.dataset.siteConfirm || "Are you sure?";
            }
            if (confirmAccept) {
                confirmAccept.textContent = confirmButton.dataset.siteConfirmAccept || "Confirm";
            }
            if (confirmCancel) {
                confirmCancel.textContent = confirmButton.dataset.siteConfirmCancel || "Go back";
            }
            confirmDialog.showModal();
        });
    });

    if (confirmAccept && confirmDialog) {
        confirmAccept.addEventListener("click", () => {
            if (!pendingConfirmedForm) return;
            const form = pendingConfirmedForm;
            pendingConfirmedForm = null;
            confirmDialog.close();
            form.submit();
        });
    }

    document.querySelectorAll("[data-confirm-close]").forEach(button => {
        button.addEventListener("click", () => {
            pendingConfirmedForm = null;
            confirmDialog?.close();
        });
    });

    if (confirmDialog) {
        confirmDialog.addEventListener("click", event => {
            if (event.target === confirmDialog) {
                pendingConfirmedForm = null;
                confirmDialog.close();
            }
        });
        confirmDialog.addEventListener("close", () => {
            pendingConfirmedForm = null;
        });
    }

    // Branded informational dialog for actions that require sign-in or membership.
    const messageDialog = document.getElementById("siteMessageDialog");
    const messageTitle = document.getElementById("siteMessageTitle");
    const messageText = document.getElementById("siteMessageText");

    document.querySelectorAll("[data-site-message]").forEach(trigger => {
        trigger.addEventListener("click", () => {
            if (!messageDialog) return;
            if (messageTitle) {
                messageTitle.textContent = trigger.dataset.siteMessageTitle || "Information";
            }
            if (messageText) {
                messageText.textContent = trigger.dataset.siteMessage || "Please sign in to continue.";
            }
            messageDialog.showModal();
        });
    });

    document.querySelectorAll("[data-message-close]").forEach(button => {
        button.addEventListener("click", () => messageDialog?.close());
    });

    if (messageDialog) {
        messageDialog.addEventListener("click", event => {
            if (event.target === messageDialog) messageDialog.close();
        });
    }

    // Event creation: calendar date picker + available 30-minute start-time slots.
    const createEventForm = document.getElementById("createEventForm");
    const eventLocation = document.getElementById("eventLocation");
    const eventDuration = document.getElementById("eventDuration");
    const eventDurationValue = document.getElementById("eventDurationValue");
    const eventLongDurationField = document.getElementById("eventLongDurationField");
    const eventLongDurationHours = document.getElementById("eventLongDurationHours");
    const eventDurationHelp = document.getElementById("eventDurationHelp");
    const eventDatePicker = document.getElementById("eventDatePicker");
    const eventDateHidden = document.getElementById("EventDate");
    const eventTimeSlots = document.getElementById("eventTimeSlots");
    const eventTimeError = document.getElementById("eventTimeError");
    let selectedEventTime = "";

    const renderEventTimes = async () => {
        if (!createEventForm || !eventLocation || !eventDuration || !eventDatePicker || !eventTimeSlots) return;

        const location = eventLocation.value;
        const duration = Number(eventDurationValue?.value || eventDuration.value);
        const date = eventDatePicker.value;
        selectedEventTime = "";
        if (eventDateHidden) eventDateHidden.value = "";
        if (eventTimeError) eventTimeError.textContent = "";

        eventTimeSlots.innerHTML = '<span class="event-time-loading">Loading available times…</span>';
        if (!date) {
            eventTimeSlots.innerHTML = '<span class="event-time-loading">Choose a date to see available times.</span>';
            return;
        }

        const url = new URL(createEventForm.dataset.availableTimesUrl, window.location.origin);
        url.searchParams.set("date", date);
        url.searchParams.set("durationMinutes", String(duration));
        url.searchParams.set("location", location);

        try {
            const response = await fetch(url);
            if (!response.ok) throw new Error("Could not load available times.");
            const times = await response.json();
            eventTimeSlots.innerHTML = "";

            if (!times.length) {
                eventTimeSlots.innerHTML = '<span class="event-time-loading">No available start times for this date and duration. Please choose another date.</span>';
                return;
            }

            times.forEach(time => {
                const button = document.createElement("button");
                button.type = "button";
                button.className = "event-time-slot";
                button.textContent = time;
                button.addEventListener("click", () => {
                    eventTimeSlots.querySelectorAll(".event-time-slot").forEach(slot => slot.classList.remove("selected"));
                    button.classList.add("selected");
                    selectedEventTime = time;
                    if (eventDateHidden) eventDateHidden.value = `${date}T${time}:00`;
                    if (eventTimeError) eventTimeError.textContent = "";
                });
                eventTimeSlots.appendChild(button);
            });
        } catch (error) {
            eventTimeSlots.innerHTML = '<span class="event-time-loading">Unable to load times. Please refresh and try again.</span>';
        }
    };

    const updateEventDurationOptions = () => {
        if (!eventLocation || !eventDuration || !eventDurationValue) return;
        const isOnline = eventLocation.value === "Online";
        eventDuration.querySelectorAll("option[data-online-only='true']").forEach(option => {
            option.disabled = !isOnline;
            option.hidden = !isOnline;
        });

        if (eventDuration.selectedOptions[0]?.disabled) {
            eventDuration.value = "180";
        }

        const isLongOnline = isOnline && eventDuration.value === "210";
        if (eventLongDurationField) eventLongDurationField.hidden = !isLongOnline;

        if (isLongOnline) {
            const hours = Number(eventLongDurationHours?.value || 3.5);
            eventDurationValue.value = String(Math.round(hours * 60));
        } else {
            eventDurationValue.value = eventDuration.value;
        }

        if (eventDurationHelp) {
            eventDurationHelp.textContent = isOnline
                ? "Choose up to 3 hours, or select 3+ hours and specify the exact duration."
                : "Library events can last from 30 minutes to 3 hours.";
        }
        renderEventTimes();
    };

    if (createEventForm && eventLocation && eventDuration && eventDatePicker) {
        eventLocation.addEventListener("change", updateEventDurationOptions);
        eventDuration.addEventListener("change", updateEventDurationOptions);
        eventLongDurationHours?.addEventListener("input", () => {
            if (eventDuration.value === "210" && eventLongDurationHours.value) {
                const minutes = Math.round(Number(eventLongDurationHours.value) * 60);
                if (Number.isFinite(minutes)) eventDurationValue.value = String(minutes);
                renderEventTimes();
            }
        });
        eventDatePicker.addEventListener("change", renderEventTimes);
        updateEventDurationOptions();

        createEventForm.addEventListener("submit", event => {
            if (!eventDateHidden?.value) {
                event.preventDefault();
                if (eventTimeError) eventTimeError.textContent = "Please choose one of the available start times.";
                eventTimeSlots.scrollIntoView({ behavior: "smooth", block: "center" });
            }
        });
    }

    // Centered Private Library toast for successful actions. It disappears after 2 seconds.
    const autoToast = document.querySelector("[data-site-toast-autoshow='true']");
    const siteToast = document.getElementById("siteToast");
    const siteToastMessage = document.getElementById("siteToastMessage");

    if (autoToast && siteToast && siteToastMessage) {
        const toastType = autoToast.dataset.siteToastType || "success";
        const toastMark = document.getElementById("siteToastMark");

        siteToastMessage.textContent = autoToast.dataset.siteToastMessage || "Your changes have been saved.";
        siteToast.classList.toggle("site-toast-error", toastType === "error");
        siteToast.classList.toggle("site-toast-success", toastType !== "error");
        if (toastMark) toastMark.textContent = toastType === "error" ? "!" : "✓";

        siteToast.hidden = false;
        requestAnimationFrame(() => siteToast.classList.add("is-visible"));

        window.setTimeout(() => {
            siteToast.classList.remove("is-visible");
            window.setTimeout(() => {
                siteToast.hidden = true;
            }, 220);
        }, 2600);
    }
});
