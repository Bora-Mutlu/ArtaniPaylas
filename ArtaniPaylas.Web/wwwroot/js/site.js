(function () {
    const toast = document.getElementById('ap-live-toast');

    function showToast(message) {
        if (!toast || !message) {
            return;
        }

        toast.textContent = message;
        toast.classList.add('show');

        window.clearTimeout(showToast._timerId);
        showToast._timerId = window.setTimeout(() => {
            toast.classList.remove('show');
        }, 3200);
    }

    function shouldReloadFor(pathFragment) {
        const current = window.location.pathname.toLowerCase();
        return current.includes(pathFragment.toLowerCase());
    }

    function isAuthPage() {
        const current = window.location.pathname.toLowerCase();
        return current.includes('/identity/account/login') || current.includes('/identity/account/register');
    }

    function isAuthenticatedUser() {
        return document.body && document.body.dataset.authenticated === 'true';
    }

    function initAuthEnhancements() {
        const emailInput = document.getElementById('Input_Email');
        const emailHelp = document.getElementById('emailLiveHelp');
        const passwordInput = document.getElementById('Input_Password');
        const strengthBar = document.getElementById('passwordStrengthBar');
        const strengthText = document.getElementById('passwordStrengthText');
        const profileImageFile = document.getElementById('Input_ProfileImageFile');
        const profileImageUrl = document.getElementById('Input_ProfileImageUrl');
        const profilePhotoChoiceHint = document.getElementById('profilePhotoChoiceHint');
        const passwordToggleButtons = document.querySelectorAll('.ap-password-toggle');

        if (emailInput && emailHelp) {
            const updateEmailHint = function () {
                const value = emailInput.value.trim();
                if (!value) {
                    emailHelp.textContent = 'Gerçek ve erişilebilir bir e-posta adresi gir.';
                    emailHelp.className = 'text-muted';
                    return;
                }

                const isValid = emailInput.checkValidity();
                emailHelp.textContent = isValid
                    ? 'E-posta formatı uygun görünüyor.'
                    : 'Geçersiz format. Örnek: ornek@mail.com';
                emailHelp.className = isValid ? 'text-success' : 'text-danger';
            };

            emailInput.addEventListener('input', updateEmailHint);
            updateEmailHint();
        }

        if (passwordInput && strengthBar && strengthText) {
            const updatePasswordStrength = function () {
                const value = passwordInput.value || '';
                const hasLength = value.length >= 6;
                const hasUpper = /[A-Z]/.test(value);
                const hasLower = /[a-z]/.test(value);
                const hasSymbol = /[^A-Za-z0-9]/.test(value);

                const score = [hasLength, hasUpper, hasLower, hasSymbol].filter(Boolean).length;
                const percent = score * 25;

                let text = 'Güvenlik: Çok Zayıf';
                let barClass = 'progress-bar bg-danger';

                if (score === 2) {
                    text = 'Güvenlik: Zayıf';
                    barClass = 'progress-bar bg-warning';
                } else if (score === 3) {
                    text = 'Güvenlik: Orta';
                    barClass = 'progress-bar bg-info';
                } else if (score === 4) {
                    text = 'Güvenlik: Güçlü';
                    barClass = 'progress-bar bg-success';
                }

                strengthBar.style.width = percent + '%';
                strengthBar.className = barClass;
                strengthText.textContent = text;
            };

            passwordInput.addEventListener('input', updatePasswordStrength);
            updatePasswordStrength();
        }

        if (profileImageFile && profileImageUrl) {
            const syncProfilePhotoInputs = function () {
                const hasFile = profileImageFile.files && profileImageFile.files.length > 0;
                if (hasFile) {
                    profileImageUrl.value = '';
                    profileImageUrl.disabled = true;
                    if (profilePhotoChoiceHint) {
                        profilePhotoChoiceHint.textContent = 'Cihazdan fotoğraf seçildi. Link alanı kilitlendi.';
                        profilePhotoChoiceHint.className = 'text-success d-block mt-1';
                    }
                    return;
                }

                profileImageUrl.disabled = false;
                if (profilePhotoChoiceHint) {
                    profilePhotoChoiceHint.textContent = 'Cihazdan fotoğraf seçersen link alanı otomatik kapanır.';
                    profilePhotoChoiceHint.className = 'text-muted d-block mt-1';
                }
            };

            profileImageFile.addEventListener('change', syncProfilePhotoInputs);
            syncProfilePhotoInputs();
        }

        if (passwordToggleButtons && passwordToggleButtons.length > 0) {
            passwordToggleButtons.forEach(function (button) {
                button.addEventListener('click', function () {
                    const targetSelector = button.getAttribute('data-target');
                    if (!targetSelector) {
                        return;
                    }

                    const input = document.querySelector(targetSelector);
                    if (!input) {
                        return;
                    }

                    const isPassword = input.getAttribute('type') === 'password';
                    input.setAttribute('type', isPassword ? 'text' : 'password');
                    button.textContent = isPassword ? 'Gizle' : 'Göster';
                });
            });
        }
    }

    initAuthEnhancements();

    if (window.signalR && isAuthenticatedUser() && !isAuthPage()) {
        const connection = new signalR.HubConnectionBuilder()
            .withUrl('/hubs/notifications')
            .withAutomaticReconnect()
            .build();

        connection.on('ListingCreated', function (payload) {
            showToast('Yeni ilan eklendi: ' + (payload && payload.title ? payload.title : 'İlan'));
            if (shouldReloadFor('/listings')) {
                setTimeout(() => window.location.reload(), 1200);
            }
        });

        connection.on('RequestCreated', function () {
            showToast('Yeni talep geldi.');
            if (shouldReloadFor('/requests/incoming') || shouldReloadFor('/profile')) {
                setTimeout(() => window.location.reload(), 1200);
            }
        });

        connection.on('RequestStatusChanged', function () {
            showToast('Talep durumun güncellendi.');
            if (shouldReloadFor('/requests/outgoing') || shouldReloadFor('/profile')) {
                setTimeout(() => window.location.reload(), 1200);
            }
        });

        connection.on('IncomingRequestStatusChanged', function () {
            showToast('Talep durumu güncellendi.');
            if (shouldReloadFor('/requests/incoming')) {
                setTimeout(() => window.location.reload(), 1200);
            }
        });

        connection.on('NewReviewReceived', function () {
            showToast('Yeni bir değerlendirme aldın.');
            if (shouldReloadFor('/profile/history') || shouldReloadFor('/profile')) {
                setTimeout(() => window.location.reload(), 1200);
            }
        });

        connection.start().catch(function () {
            // silently ignore when user is logged out or connection is unavailable
        });
    }
})();
