
(function () {
    const HOME_LOADER_SESSION_KEY = 'nm:home-loader-shown';

    // Chỉ chạy khi loader tồn tại (tức là trang chủ)
    const loader = document.getElementById('nanny-loader');
    if (!loader) return;

    // Chỉ hiện một lần cho mỗi phiên duyệt tab hiện tại.
    // Khi người dùng vào login rồi quay lại trang chủ trong cùng phiên,
    // loader sẽ không chạy lại.
    if (sessionStorage.getItem(HOME_LOADER_SESSION_KEY) === '1') {
        loader.style.display = 'none';
        return;
    }

    sessionStorage.setItem(HOME_LOADER_SESSION_KEY, '1');

    const fillBar = document.getElementById('nl-fillBar');
    const statusText = document.getElementById('nl-statusText');
    const statusPct = document.getElementById('nl-statusPct');
    const tipText = document.getElementById('nl-tipText');

    const steps = [
        { pct: 15, msg: 'Đang khởi động...', tip: '💡 Mẹo: <span>Tìm bảo mẫu đã xác minh gần bạn chỉ trong 30 giây.</span>' },
        { pct: 35, msg: 'Kết nối cơ sở dữ liệu...', tip: '🌟 <span>Hơn 10.000 gia đình đã tin dùng NannyMatch!</span>' },
        { pct: 55, msg: 'Tải hồ sơ người trông trẻ...', tip: '🔒 <span>Mọi bảo mẫu đều được kiểm tra lý lịch cẩn thận.</span>' },
        { pct: 72, msg: 'Đồng bộ lịch trình...', tip: '📅 <span>Đặt lịch linh hoạt: theo giờ, ngày hoặc dài hạn.</span>' },
        { pct: 88, msg: 'Khởi tạo giao diện...', tip: '💳 <span>Thanh toán an toàn, hoàn tiền nếu không hài lòng.</span>' },
        { pct: 100, msg: 'Hoàn tất! Chào mừng bạn 🎉', tip: '🏠 <span>NannyMatch — Yên tâm giao con, an lòng cha mẹ.</span>' },
    ];

    let stepIndex = 0;

    function swapText(msg, tip) {
        statusText.style.opacity = '0';
        tipText.style.opacity = '0';
        setTimeout(function () {
            statusText.textContent = msg;
            tipText.innerHTML = tip;
            statusText.style.transition = 'opacity .25s';
            tipText.style.transition = 'opacity .25s';
            statusText.style.opacity = '1';
            tipText.style.opacity = '1';
        }, 150);
    }

    function advance() {
        if (stepIndex >= steps.length) return;
        var s = steps[stepIndex++];
        fillBar.style.width = s.pct + '%';
        statusPct.textContent = s.pct + '%';
        swapText(s.msg, s.tip);

        if (stepIndex < steps.length) {
            // Giảm từ 300-700ms xuống 150-350ms → nhanh hơn ~2x
            setTimeout(advance, Math.random() * 200 + 150);
        } else {
            // Hoàn tất → Fade Out nhanh hơn
            setTimeout(function () {
                loader.classList.add('exit-fade');
                setTimeout(function () {
                    loader.style.display = 'none';
                }, 600);
            }, 400);
        }
    }

    // Boot nhanh hơn: 200ms thay vì 600ms
    setTimeout(advance, 200);
})();



/* ══════════════════════════════════════════════════
   Scroll-reveal: xen kê hiệu ứng lên/xuống theo từng section
   ══════════════════════════════════════════════════ */
(function () {
    /* 1. Phần tử .reveal (phần hero đã có CSS; các .reveal phía dưới cần JS) */
    var revealEls = document.querySelectorAll('.reveal');
    var revealObserver = new IntersectionObserver(function (entries) {
        entries.forEach(function (entry) {
            if (entry.isIntersecting) {
                entry.target.classList.add('visible');
                revealObserver.unobserve(entry.target);
            }
        });
    }, { threshold: 0.15 });
    revealEls.forEach(function (el) { revealObserver.observe(el); });

    /* 2. .scroll-section — thêm in-view khi vào khoảng 20% viewport */
    var sections = document.querySelectorAll('.scroll-section');
    var sectionObserver = new IntersectionObserver(function (entries) {
        entries.forEach(function (entry) {
            if (entry.isIntersecting) {
                entry.target.classList.add('in-view');
                sectionObserver.unobserve(entry.target);
            }
        });
    }, { threshold: 0.18 });
    sections.forEach(function (sec) { sectionObserver.observe(sec); });
})();





