
(function () {
    // Chỉ chạy khi loader tồn tại (tức là trang chủ)
    const loader = document.getElementById('nanny-loader');
    if (!loader) return;

    const fillBar = document.getElementById('nl-fillBar');
    const statusText = document.getElementById('nl-statusText');
    const statusPct = document.getElementById('nl-statusPct');
    const tipText = document.getElementById('nl-tipText');

    const steps = [
        { pct: 15, msg: 'Đang khởi động...', tip: '💡 Mẹo: <span>Tìm Nanny đã xác minh gần bạn chỉ trong 30 giây.</span>' },
        { pct: 35, msg: 'Kết nối cơ sở dữ liệu...', tip: '🌟 <span>Hơn 10,000 gia đình đã tin dùng NannyMatch!</span>' },
        { pct: 55, msg: 'Tải hồ sơ người trông trẻ...', tip: '🔒 <span>Tất cả Nanny đều được kiểm tra lý lịch kỹ càng.</span>' },
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
   Scroll-reveal: alternating up/down per section
   ══════════════════════════════════════════════════ */
(function () {
    /* 1. Handle .reveal elements (hero already animates via CSS,
          but other .reveal below fold need JS) */
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

    /* 2. Handle .scroll-section — add in-view when 20% visible */
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





tailwind.config = {
    darkMode: "class",
    theme: {
        extend: {
            colors: {
                primary: "#F97316",
                primaryDark: "#EA580C",
                "background-light": "#F8F9FC",
                "background-dark": "#0F172A",
                "card-light": "#FFFFFF",
                "card-dark": "#1E293B",
                gold: "#EAB308",
            },
            fontFamily: {
                display: ["Quicksand", "sans-serif"],
                body: ["Inter", "sans-serif"],
            },
            borderRadius: {
                DEFAULT: "0.75rem",
                'sm': '0.5rem',
                'md': '0.75rem',
                'lg': '1rem',
                'xl': '1.25rem',
                '2xl': '1.5rem',
                '3xl': '2rem',
                'full': '9999px',
            },
            boxShadow: {
                'soft': '0 2px 15px -3px rgba(0,0,0,.07), 0 10px 20px -2px rgba(0,0,0,.04)',
                'card': '0 1px 3px rgba(0,0,0,.06), 0 4px 16px rgba(0,0,0,.04)',
                'glow': '0 0 24px rgba(249,115,22,.25)',
            },
        },
    },
};



