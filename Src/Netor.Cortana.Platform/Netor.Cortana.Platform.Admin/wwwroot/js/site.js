(() => {
	const sidebar = document.getElementById("adminSidebar");
	const toggle = document.querySelector("[data-admin-toggle='sidebar']");

	if (!sidebar || !toggle) {
		return;
	}

	toggle.addEventListener("click", () => {
		sidebar.classList.toggle("open");
	});
})();

(() => {
	const masters = document.querySelectorAll("[data-check-all]");

	masters.forEach((master) => {
		const target = master.getAttribute("data-check-all");
		if (!target) {
			return;
		}

		master.addEventListener("change", () => {
			document.querySelectorAll(`[data-check-item='${target}']`).forEach((item) => {
				item.checked = master.checked;
			});
		});
	});
})();

(() => {
	if (!window.layui) {
		return;
	}

	layui.use(["element", "form"], function () {
		const element = layui.element;
		const form = layui.form;
		element.render();
		form.render();

		const checkAll = document.querySelectorAll("[data-check-all]");
		checkAll.forEach((item) => {
			item.addEventListener("change", () => {
				const target = item.getAttribute("data-check-all");
				if (!target) {
					return;
				}

				document.querySelectorAll(`[data-check-item='${target}']`).forEach((checkbox) => {
					checkbox.checked = item.checked;
				});
			});
		});
	});
})();
