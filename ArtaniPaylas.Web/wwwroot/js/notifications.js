// ArtaniPaylas Notifications JS - Toastr Helper Functions

// Toastr default configuration
if (typeof toastr !== 'undefined') {
    toastr.options = {
        "closeButton": true,
        "debug": false,
        "newestOnTop": true,
        "progressBar": true,
        "positionClass": "toast-top-right",
        "preventDuplicates": false,
        "onclick": null,
        "showDuration": 300,
        "hideDuration": 1000,
        "timeOut": 3000,
        "extendedTimeOut": 1000,
        "showEasing": "swing",
        "hideEasing": "linear",
        "showMethod": "fadeIn",
        "hideMethod": "fadeOut"
    };
}

/**
 * Başarılı bildirim göster
 * @param {string} title - Bildirim başlığı
 * @param {string} message - Bildirim mesajı
 * @param {number} duration - Gösterim süresi (ms), default: 3000
 */
function showSuccessToast(title, message, duration = 3000) {
    if (typeof toastr === 'undefined') {
        console.log(`Success: ${title} - ${message}`);
        return;
    }
    
    toastr.options.timeOut = duration;
    toastr.success(message, title);
}

/**
 * Hata bildirim göster
 * @param {string} title - Bildirim başlığı
 * @param {string} message - Bildirim mesajı
 * @param {number} duration - Gösterim süresi (ms), default: 5000
 */
function showErrorToast(title, message, duration = 5000) {
    if (typeof toastr === 'undefined') {
        console.error(`Error: ${title} - ${message}`);
        return;
    }
    
    toastr.options.timeOut = duration;
    toastr.error(message, title);
}

/**
 * Bilgi bildirim göster
 * @param {string} title - Bildirim başlığı
 * @param {string} message - Bildirim mesajı
 * @param {number} duration - Gösterim süresi (ms), default: 3000
 */
function showInfoToast(title, message, duration = 3000) {
    if (typeof toastr === 'undefined') {
        console.info(`Info: ${title} - ${message}`);
        return;
    }
    
    toastr.options.timeOut = duration;
    toastr.info(message, title);
}

/**
 * Uyarı bildirim göster
 * @param {string} title - Bildirim başlığı
 * @param {string} message - Bildirim mesajı
 * @param {number} duration - Gösterim süresi (ms), default: 4000
 */
function showWarningToast(title, message, duration = 4000) {
    if (typeof toastr === 'undefined') {
        console.warn(`Warning: ${title} - ${message}`);
        return;
    }
    
    toastr.options.timeOut = duration;
    toastr.warning(message, title);
}

/**
 * Genel bildirim göster
 * @param {string} message - Bildirim mesajı
 * @param {string} type - Tip: 'success', 'error', 'info', 'warning'
 * @param {number} duration - Gösterim süresi (ms), default: 3000
 */
function showToast(message, type = 'info', duration = 3000) {
    switch (type.toLowerCase()) {
        case 'success':
            showSuccessToast('Başarılı', message, duration);
            break;
        case 'error':
            showErrorToast('Hata', message, duration);
            break;
        case 'warning':
            showWarningToast('Uyarı', message, duration);
            break;
        case 'info':
        default:
            showInfoToast('Bilgi', message, duration);
            break;
    }
}

/**
 * TempData bildirimlerini otomatik göster (server-side messages)
 */
document.addEventListener('DOMContentLoaded', function () {
    // Success message
    const successElement = document.querySelector('[data-notification-type="success"]');
    if (successElement && successElement.textContent.trim()) {
        showSuccessToast('Başarılı ✓', successElement.textContent.trim());
    }

    // Error message
    const errorElement = document.querySelector('[data-notification-type="error"]');
    if (errorElement && errorElement.textContent.trim()) {
        showErrorToast('Hata', errorElement.textContent.trim(), 5000);
    }

    // Info message
    const infoElement = document.querySelector('[data-notification-type="info"]');
    if (infoElement && infoElement.textContent.trim()) {
        showInfoToast('Bilgi', infoElement.textContent.trim());
    }

    // Warning message
    const warningElement = document.querySelector('[data-notification-type="warning"]');
    if (warningElement && warningElement.textContent.trim()) {
        showWarningToast('Uyarı', warningElement.textContent.trim());
    }
});

/**
 * Notification dropdown açıp kapat
 */
function toggleNotificationDropdown() {
    const dropdown = document.querySelector('.notification-dropdown');
    if (dropdown) {
        const isOpen = dropdown.style.display === 'block';
        dropdown.style.display = isOpen ? 'none' : 'block';
        if (!isOpen) {
            loadLatestNotifications();
        }
    }
}

/**
 * Notification'u okundu olarak işaretle (AJAX)
 * @param {number} notificationId - Bildirim ID
 */
function markNotificationAsRead(notificationId, redirectUrl) {
    const url = '/UserDashboard/MarkNotificationAsRead';
    
    fetch(url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ notificationId: notificationId })
    })
    .then(response => {
        if (response.ok) {
            // Mark as read in UI
            const item = document.querySelector(`[data-notification-id="${notificationId}"]`);
            if (item) {
                item.classList.remove('unread');
            }
            
            // Update unread count
            updateUnreadNotificationCount();
            if (redirectUrl) {
                window.location.href = redirectUrl;
            }
        }
    })
    .catch(error => console.error('Error marking notification as read:', error));
}

/**
 * Okunmamış bildirim sayısını güncelle
 */
function updateUnreadNotificationCount() {
    const badge = document.querySelector('.notification-badge');
    if (!badge) return;

    const url = '/UserDashboard/GetUnreadNotificationCount';
    
    fetch(url)
    .then(response => response.json())
    .then(data => {
        if ((data.unreadCount ?? 0) > 0) {
            badge.textContent = data.unreadCount;
            badge.style.display = 'block';
        } else {
            badge.style.display = 'none';
        }
    })
    .catch(error => console.error('Error updating unread count:', error));
}

function loadLatestNotifications() {
    const list = document.getElementById('notificationList');
    if (!list) return;

    fetch('/UserDashboard/GetLatestNotifications?limit=10')
        .then(response => response.json())
        .then(items => {
            if (!Array.isArray(items) || items.length === 0) {
                list.innerHTML = '<div class="px-2 py-2 text-muted small">Henüz bildiriminiz yok.</div>';
                return;
            }

            const defaultRedirect = '/Requests/Incoming';
            list.innerHTML = items.map(item => {
                const unreadClass = item.isRead ? '' : 'background:#f2f7ff;';
                return `
                    <button type="button" onclick="markNotificationAsRead(${item.id}, '${defaultRedirect}')" data-notification-id="${item.id}" class="text-start border-0 bg-transparent w-100 p-0">
                        <div class="px-2 py-2 rounded" style="${unreadClass}">
                            <div class="fw-semibold small">${item.title ?? 'Bildirim'}</div>
                            <div class="small text-muted">${item.message ?? ''}</div>
                        </div>
                    </button>`;
            }).join('');
        })
        .catch(() => {
            list.innerHTML = '<div class="px-2 py-2 text-danger small">Bildirimler alınamadı.</div>';
        });
}

// Sayfa yüklendiğinde okunmamış bildirim sayısını güncelle
document.addEventListener('DOMContentLoaded', function () {
    updateUnreadNotificationCount();
});

// Document dışında tıklandığında dropdown'ı kapat
document.addEventListener('click', function (event) {
    const notificationContainer = document.querySelector('.notification-icon');
    const dropdown = document.querySelector('.notification-dropdown');
    
    if (notificationContainer && !notificationContainer.contains(event.target) && dropdown) {
        dropdown.style.display = 'none';
    }
});

