(() => {
    const phoneInput = document.getElementById("Input_PhoneNumber");
    const ageInput = document.getElementById("Input_Age");
    const fileInput = document.getElementById("Input_ProfileImageFile");
    const urlInput = document.getElementById("Input_ProfileImageUrl");
    const urlGroup = document.getElementById("profileImageUrlGroup");
    const photoChoiceHint = document.getElementById("profilePhotoChoiceHint");
    const clearImageChoiceButton = document.getElementById("clearProfileImageFileButton");
    const passwordInput = document.getElementById("Input_Password");
    const passwordStrengthBar = document.getElementById("passwordStrengthBar");
    const passwordStrengthText = document.getElementById("passwordStrengthText");

    function keepOnlyDigits(input, maxLength) {
        if (!input) {
            return;
        }

        input.value = input.value.replace(/\D/g, "").slice(0, maxLength);
    }

    if (phoneInput) {
        phoneInput.addEventListener("input", () => keepOnlyDigits(phoneInput, 11));
    }

    if (ageInput) {
        ageInput.addEventListener("input", () => keepOnlyDigits(ageInput, 2));
    }

    function syncImageChoiceState() {
        if (!fileInput || !urlInput || !photoChoiceHint) {
            return;
        }

        const hasFile = Boolean(fileInput.files && fileInput.files.length > 0);
        const hasUrl = urlInput.value.trim().length > 0;

        if (hasFile) {
            urlInput.value = "";
            urlInput.disabled = true;
            if (urlGroup) {
                urlGroup.classList.add("ap-input-disabled");
            }
            photoChoiceHint.textContent = "Dosya secildi. Baglanti alani kapatildi.";
            if (clearImageChoiceButton) {
                clearImageChoiceButton.classList.remove("d-none");
            }
            return;
        }

        if (hasUrl) {
            fileInput.disabled = true;
            photoChoiceHint.textContent = "Baglanti girildi. Dosya alani kapatildi.";
            if (clearImageChoiceButton) {
                clearImageChoiceButton.classList.remove("d-none");
            }
            return;
        }

        fileInput.disabled = false;
        urlInput.disabled = false;
        if (urlGroup) {
            urlGroup.classList.remove("ap-input-disabled");
        }
        photoChoiceHint.textContent = "Cihazdan fotograf secersen link alani otomatik kapanir.";
        if (clearImageChoiceButton) {
            clearImageChoiceButton.classList.add("d-none");
        }
    }

    if (fileInput) {
        fileInput.addEventListener("change", syncImageChoiceState);
    }

    if (urlInput) {
        urlInput.addEventListener("input", syncImageChoiceState);
    }

    if (clearImageChoiceButton && fileInput && urlInput) {
        clearImageChoiceButton.addEventListener("click", () => {
            fileInput.value = "";
            fileInput.disabled = false;
            urlInput.value = "";
            urlInput.disabled = false;
            syncImageChoiceState();
        });
    }

    syncImageChoiceState();

    function bindPasswordToggles() {
        document.querySelectorAll(".ap-password-toggle").forEach((button) => {
            button.addEventListener("click", () => {
                const targetSelector = button.getAttribute("data-target");
                if (!targetSelector) {
                    return;
                }

                const targetInput = document.querySelector(targetSelector);
                if (!(targetInput instanceof HTMLInputElement)) {
                    return;
                }

                const reveal = targetInput.type === "password";
                targetInput.type = reveal ? "text" : "password";
                button.textContent = reveal ? "Gizle" : "Goster";
                button.setAttribute("aria-label", reveal ? "Sifreyi gizle" : "Sifreyi goster");
                button.setAttribute("aria-pressed", reveal ? "true" : "false");
            });
        });
    }

    function scorePassword(value) {
        let score = 0;

        if (!value) {
            return 0;
        }

        if (value.length >= 6) score += 20;
        if (value.length >= 10) score += 20;
        if (/[a-z]/.test(value)) score += 15;
        if (/[A-Z]/.test(value)) score += 15;
        if (/\d/.test(value)) score += 10;
        if (/[^a-zA-Z0-9]/.test(value)) score += 20;

        return Math.min(score, 100);
    }

    function paintStrength(score) {
        if (!passwordStrengthBar || !passwordStrengthText) {
            return;
        }

        passwordStrengthBar.style.width = `${score}%`;
        passwordStrengthBar.classList.remove("ap-strength-low", "ap-strength-medium", "ap-strength-high");

        if (score < 40) {
            passwordStrengthBar.classList.add("ap-strength-low");
            passwordStrengthText.textContent = "Guvenlik: Zayif";
            return;
        }

        if (score < 75) {
            passwordStrengthBar.classList.add("ap-strength-medium");
            passwordStrengthText.textContent = "Guvenlik: Orta";
            return;
        }

        passwordStrengthBar.classList.add("ap-strength-high");
        passwordStrengthText.textContent = "Guvenlik: Guclu";
    }

    if (passwordInput) {
        passwordInput.addEventListener("input", () => {
            paintStrength(scorePassword(passwordInput.value));
        });

        paintStrength(scorePassword(passwordInput.value));
    }

    bindPasswordToggles();
})();
