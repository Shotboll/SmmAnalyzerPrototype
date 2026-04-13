// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

window.initLoadingForm = function (options) {
    const form = document.getElementById(options.formId);
    if (!form) return;

    const submitBtn = document.getElementById(options.submitButtonId);
    const overlay = document.getElementById(options.overlayId);
    const messageEl = document.getElementById(options.messageId);
    const textArea = options.textAreaId ? document.getElementById(options.textAreaId) : null;
    const selectEl = options.selectId ? document.getElementById(options.selectId) : null;
    const charCounter = options.charCounterId ? document.getElementById(options.charCounterId) : null;

    const messages = Array.isArray(options.messages) ? options.messages : [];
    const submitText = options.submitText || "Отправить";
    const loadingButtonText = options.loadingButtonText || "Выполняется...";
    const maxLength = options.maxLength || 5000;

    let loadingInterval = null;

    function updateCharCounter() {
        if (!textArea || !charCounter) return;
        charCounter.textContent = textArea.value.length.toString();
    }

    function startLoadingMessages() {
        if (!messageEl || messages.length === 0) return;

        let index = 0;
        messageEl.textContent = messages[index];

        loadingInterval = setInterval(() => {
            index++;
            if (index < messages.length) {
                messageEl.textContent = messages[index];
            } else {
                clearInterval(loadingInterval);
                loadingInterval = null;
            }
        }, 7000);
    }

    function stopLoadingMessages() {
        if (loadingInterval) {
            clearInterval(loadingInterval);
            loadingInterval = null;
        }
    }

    function showLoading() {
        if (overlay) {
            overlay.classList.remove("d-none");
        }

        document.body.classList.add("loading-active");

        if (submitBtn) {
            submitBtn.disabled = true;
            submitBtn.textContent = loadingButtonText;
        }

        startLoadingMessages();
    }

    function hideLoading() {
        if (overlay) {
            overlay.classList.add("d-none");
        }

        document.body.classList.remove("loading-active");

        if (submitBtn) {
            submitBtn.disabled = false;
            submitBtn.textContent = submitText;
        }

        stopLoadingMessages();
    }

    if (textArea) {
        updateCharCounter();
        textArea.addEventListener("input", updateCharCounter);
    }

    form.addEventListener("submit", function () {
        const text = textArea ? textArea.value.trim() : "";
        const selectedValue = selectEl ? selectEl.value : "";

        if (selectEl && !selectedValue) {
            hideLoading();
            return;
        }

        if (textArea) {
            if (!text || text.length > maxLength) {
                hideLoading();
                return;
            }
        }

        showLoading();
    });

    window.addEventListener("pageshow", function () {
        hideLoading();
    });
};